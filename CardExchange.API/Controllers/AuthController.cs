using BCrypt.Net;
using CardExchange.API.Configuration;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CardExchange.Infrastructure.Data;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly JwtSettings _jwtSettings;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IUserRepository userRepository,
            ITokenService tokenService,
            IOptions<JwtSettings> jwtSettings,
             ApplicationDbContext context,
            ILogger<AuthController> logger)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _jwtSettings = jwtSettings.Value;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Registrazione nuovo utente
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Verifica che email e username siano univoci
                if (await _userRepository.EmailExistsAsync(request.Email))
                {
                    return BadRequest(new { message = "Email già in uso" });
                }

                if (await _userRepository.UsernameExistsAsync(request.Username))
                {
                    return BadRequest(new { message = "Username già in uso" });
                }

                // Hash della password con BCrypt
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

                // Crea il nuovo utente
                // Crea il nuovo utente
                var user = new User
                {
                    Email = request.Email,
                    Username = request.Username,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Bio = request.Bio,
                    PasswordHash = passwordHash,
                    IsActive = true,
                    EmailConfirmed = false,
                    RefreshToken = _tokenService.GenerateRefreshToken(),
                    RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)
                };

                await _userRepository.CreateAsync(user);
                await _userRepository.SaveChangesAsync();

                // ========== AGGIUNGI QUESTA SEZIONE ==========
                // Assegna il ruolo "User" di default
                var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
                if (userRole != null)
                {
                    var userRoleAssignment = new UserRole
                    {
                        UserId = user.Id,
                        RoleId = userRole.Id,
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = null // Auto-assegnato alla registrazione
                    };

                    await _context.UserRoles.AddAsync(userRoleAssignment);
                    await _context.SaveChangesAsync();
                }
                // ==========================================

                _logger.LogInformation("Nuovo utente registrato: {UserId} - {Username}", user.Id, user.Username);

                // Genera i token
                var accessToken = _tokenService.GenerateAccessToken(user);
                var accessTokenExpiration = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

                var response = new RegisterResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = user.RefreshToken,
                    AccessTokenExpiration = accessTokenExpiration,
                    RefreshTokenExpiration = user.RefreshTokenExpiryTime!.Value,
                    User = MapToUserDto(user),
                    Message = "Registrazione completata con successo"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la registrazione dell'utente");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Login utente esistente
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                // Cerca l'utente per email o username
                User? user = null;

                if (request.UsernameOrEmail.Contains("@"))
                {
                    user = await _userRepository.GetByEmailAsync(request.UsernameOrEmail);
                }
                else
                {
                    user = await _userRepository.GetByUsernameAsync(request.UsernameOrEmail);
                }

                if (user == null)
                {
                    return Unauthorized(new { message = "Credenziali non valide" });
                }

                // Verifica che l'utente sia attivo
                if (!user.IsActive)
                {
                    return Unauthorized(new { message = "Account disattivato" });
                }

                // Verifica la password
                if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    return Unauthorized(new { message = "Credenziali non valide" });
                }

                // Genera nuovo refresh token
                user.RefreshToken = _tokenService.GenerateRefreshToken();
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
                user.LastLoginAt = DateTime.UtcNow;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("Login effettuato per l'utente: {UserId} - {Username}", user.Id, user.Username);

                // Genera access token
                var accessToken = _tokenService.GenerateAccessToken(user);
                var accessTokenExpiration = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

                // Carica la location se esiste
                var userWithLocation = await _userRepository.GetWithLocationAsync(user.Id);

                var response = new LoginResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = user.RefreshToken,
                    AccessTokenExpiration = accessTokenExpiration,
                    RefreshTokenExpiration = user.RefreshTokenExpiryTime!.Value,
                    User = MapToUserDto(userWithLocation!),
                    Message = "Login effettuato con successo"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il login");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Rinnova l'access token usando il refresh token
        /// </summary>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                // Valida l'access token scaduto e recupera i claims
                var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);

                if (principal == null)
                {
                    return BadRequest(new { message = "Token non valido" });
                }

                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return BadRequest(new { message = "Token non valido" });
                }

                var userId = int.Parse(userIdClaim.Value);
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "Utente non trovato" });
                }

                // Valida il refresh token
                if (!await _tokenService.ValidateRefreshToken(user, request.RefreshToken))
                {
                    return Unauthorized(new { message = "Refresh token non valido o scaduto" });
                }

                // Genera nuovi token
                var newAccessToken = _tokenService.GenerateAccessToken(user);
                var newRefreshToken = _tokenService.GenerateRefreshToken();

                user.RefreshToken = newRefreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("Token rinnovato per l'utente: {UserId}", userId);

                var accessTokenExpiration = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

                // Carica la location
                var userWithLocation = await _userRepository.GetWithLocationAsync(user.Id);

                var response = new AuthResponse
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken,
                    AccessTokenExpiration = accessTokenExpiration,
                    RefreshTokenExpiration = user.RefreshTokenExpiryTime!.Value,
                    User = MapToUserDto(userWithLocation!)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il refresh del token");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Logout utente (invalida il refresh token)
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult> Logout()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Unauthorized();
                }

                var userId = int.Parse(userIdClaim.Value);
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "Utente non trovato" });
                }

                // Invalida il refresh token
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("Logout effettuato per l'utente: {UserId}", userId);

                return Ok(new { message = "Logout effettuato con successo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il logout");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene le informazioni dell'utente corrente autenticato
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Unauthorized();
                }

                var userId = int.Parse(userIdClaim.Value);
                var user = await _userRepository.GetWithLocationAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "Utente non trovato" });
                }

                return Ok(MapToUserDto(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dell'utente corrente");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Cambia la password dell'utente autenticato
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Unauthorized();
                }

                var userId = int.Parse(userIdClaim.Value);
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "Utente non trovato" });
                }

                // Verifica la password corrente
                if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                {
                    return BadRequest(new { message = "Password corrente non valida" });
                }

                // Aggiorna la password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

                // Invalida tutti i refresh token per sicurezza
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("Password cambiata per l'utente: {UserId}", userId);

                return Ok(new { message = "Password cambiata con successo. Effettua nuovamente il login." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il cambio password");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        // Helper method
        private static UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Bio = user.Bio,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                Location = user.Location != null ? new UserLocationDto
                {
                    City = user.Location.City,
                    Province = user.Location.Province,
                    Country = user.Location.Country,
                    PostalCode = user.Location.PostalCode,
                    Latitude = user.Location.Latitude,
                    Longitude = user.Location.Longitude,
                    MaxDistanceKm = user.Location.MaxDistanceKm
                } : null
            };
        }
    }

    // DTO per cambio password
    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "La password corrente è obbligatoria")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nuova password è obbligatoria")]
        [MinLength(8, ErrorMessage = "La password deve essere di almeno 8 caratteri")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "La password deve contenere almeno una maiuscola, una minuscola, un numero e un carattere speciale")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La conferma password è obbligatoria")]
        [Compare("NewPassword", ErrorMessage = "Le password non coincidono")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}