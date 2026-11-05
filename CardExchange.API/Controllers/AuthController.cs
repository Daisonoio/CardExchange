using BCrypt.Net;
using CardExchange.API.Configuration;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.API.Services;
using CardExchange.API.Services.Email;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // Limite rigido solo sulle azioni sensibili (vedi sotto): /me e logout vengono chiamati a ogni pagina
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly JwtSettings _jwtSettings;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly INotificationService _notificationService;
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<AuthController> _logger;

        private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

        public AuthController(
            IUserRepository userRepository,
            ITokenService tokenService,
            IOptions<JwtSettings> jwtSettings,
            ApplicationDbContext context,
            IEmailSender emailSender,
            IOptions<EmailSettings> emailSettings,
            INotificationService notificationService,
            ILogger<AuthController> logger)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _jwtSettings = jwtSettings.Value;
            _context = context;
            _emailSender = emailSender;
            _notificationService = notificationService;
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Registrazione nuovo utente
        /// {
        //  "email": "lucameli.lm@gmail.com", "mariorossi@gmail.com"
        //  "username": "DaisonoIo",
        //  "firstName": "Luca",
        //  "lastName": "Meli",
        //  "bio": "Yeuiuhis",
        //  "password": "Password123!",
        //  "confirmPassword": "Password123!"
        //}
        /// </summary>
        [HttpPost("register")]
        [EnableRateLimiting("AuthPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request,
            [FromServices] IConfiguration? configuration = null)
        {
            try
            {
                // Età minima (14 anni in Italia): sotto servirebbe il consenso verificabile dei genitori
                var minimumAge = MinimumAge(configuration);
                if (request.BirthDate == null)
                    return BadRequest(new { message = "La data di nascita è obbligatoria" });
                if (!AgeRules.MeetsMinimum(request.BirthDate.Value, DateOnly.FromDateTime(DateTime.UtcNow), minimumAge))
                    return BadRequest(new { message = $"Per registrarti a CardExchange devi avere almeno {minimumAge} anni.", code = "UNDERAGE" });

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
                var emailVerificationToken = Guid.NewGuid().ToString("N");

                // Invito: un codice non valido non blocca la registrazione, viene semplicemente ignorato
                var referralCode = ReferralCodes.Normalize(request.ReferralCode);
                var referrer = referralCode == null ? null
                    : await _context.Users.FirstOrDefaultAsync(u => u.ReferralCode == referralCode);

                var user = new User
                {
                    Email = request.Email,
                    Username = request.Username,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Bio = request.Bio,
                    BirthDate = request.BirthDate,
                    PasswordHash = passwordHash,
                    IsActive = true,
                    EmailConfirmed = false,
                    EmailVerificationToken = emailVerificationToken,
                    EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
                    ReferredByUserId = referrer?.Id,
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

                // Registrazione dal QR di un negozio: l'utente è legato al negozio e lo segue
                await AttributeToStoreAsync(user.Id, request.StoreSlug);

                await SendVerificationEmailAsync(user);

                if (referrer != null)
                {
                    try
                    {
                        await _notificationService.SendAsync(referrer.Id, NotificationType.SystemAnnouncement,
                            "Un amico si è iscritto", $"{user.Username} si è iscritto con il tuo invito. Grazie per far crescere la community!",
                            user.Id, "User");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Notifica invito non inviata a {ReferrerId}", referrer.Id);
                    }
                }

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
        [EnableRateLimiting("AuthPolicy")]
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
                    // Gli account sospesi sono esclusi dal filtro globale: lo si dice solo a chi conosce la password
                    var suspended = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u =>
                        !u.IsActive && (u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail));
                    if (suspended != null && BCrypt.Net.BCrypt.Verify(request.Password, suspended.PasswordHash))
                    {
                        if (suspended.BirthDate != null && !AgeRules.MeetsMinimum(suspended.BirthDate.Value, DateOnly.FromDateTime(DateTime.UtcNow), MinimumAge(null)))
                            return Unauthorized(new { message = $"Per usare CardExchange devi avere almeno {MinimumAge(null)} anni. Per cancellare i tuoi dati scrivi al supporto.", code = "UNDERAGE" });
                        return Unauthorized(new { message = "Account sospeso. Contatta il supporto se pensi sia un errore." });
                    }
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
        [EnableRateLimiting("AuthPolicy")]
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

        /// <summary>
        /// Verifica l'email dell'utente tramite token
        /// </summary>
        [HttpGet("verify-email")]
        [EnableRateLimiting("AuthPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult> VerifyEmail([FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return BadRequest(new { message = "Token di verifica mancante" });
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.EmailVerificationToken == token);

                if (user == null)
                {
                    return BadRequest(new { message = "Token di verifica non valido" });
                }

                if (user.EmailVerificationTokenExpiry < DateTime.UtcNow)
                {
                    return BadRequest(new { message = "Token di verifica scaduto. Richiedi un nuovo link di verifica." });
                }

                user.EmailConfirmed = true;
                user.EmailVerificationToken = null;
                user.EmailVerificationTokenExpiry = null;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("Email verificata per l'utente: {UserId} - {Email}", user.Id, user.Email);

                return Ok(new { message = "Email verificata con successo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la verifica email");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Rigenera il token di verifica email
        /// </summary>
        [HttpPost("resend-verification")]
        [EnableRateLimiting("AuthPolicy")]
        [Authorize]
        public async Task<ActionResult> ResendVerificationEmail()
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

                if (user.EmailConfirmed)
                {
                    return BadRequest(new { message = "Email già verificata" });
                }

                user.EmailVerificationToken = Guid.NewGuid().ToString("N");
                user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("Token verifica email rigenerato per utente: {UserId}", userId);

                await SendVerificationEmailAsync(user);

                // Il token non viene mai restituito nella risposta: arriva solo via email
                return Ok(new { message = "Ti abbiamo inviato un nuovo link di verifica via email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la rigenerazione del token di verifica");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Avvia il recupero password. Risponde sempre allo stesso modo per non rivelare quali email sono registrate.
        /// </summary>
        [HttpPost("forgot-password")]
        [EnableRateLimiting("AuthPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            const string genericMessage = "Se l'indirizzo è registrato, riceverai a breve un'email con le istruzioni per reimpostare la password.";
            try
            {
                var user = await _userRepository.GetByEmailAsync(request.Email.Trim());
                if (user == null || !user.IsActive)
                {
                    return Ok(new { message = genericMessage });
                }

                var token = GenerateUrlSafeToken();
                user.PasswordResetTokenHash = HashToken(token);
                user.PasswordResetTokenExpiry = DateTime.UtcNow.Add(PasswordResetTokenLifetime);

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                var link = $"{_emailSettings.FrontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";
                await TrySendEmailAsync(user, EmailTemplates.ResetPassword(user.FirstName, link));

                _logger.LogInformation("Richiesta reset password per utente {UserId}", user.Id);
                return Ok(new { message = genericMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la richiesta di reset password");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Imposta una nuova password usando il token ricevuto via email (monouso, valido 1 ora).
        /// </summary>
        [HttpPost("reset-password")]
        [EnableRateLimiting("AuthPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var tokenHash = HashToken(request.Token);
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.PasswordResetTokenHash == tokenHash);

                if (user == null || user.PasswordResetTokenExpiry == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
                {
                    return BadRequest(new { message = "Link di reset non valido o scaduto. Richiedine uno nuovo." });
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                user.PasswordResetTokenHash = null;
                user.PasswordResetTokenExpiry = null;

                // Chi ha ricevuto il link ha dimostrato di controllare la casella email
                user.EmailConfirmed = true;

                // Disconnette tutte le sessioni esistenti
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("Password reimpostata tramite email per utente {UserId}", user.Id);
                return Ok(new { message = "Password reimpostata. Ora puoi accedere con la nuova password." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il reset della password");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Lega il nuovo utente al negozio del QR (solo negozi verificati, mai il proprietario stesso) e glielo fa seguire.
        /// Un errore qui non deve bloccare la registrazione.
        /// </summary>
        private async Task AttributeToStoreAsync(int userId, string? storeSlug)
        {
            if (string.IsNullOrWhiteSpace(storeSlug)) return;
            try
            {
                var slug = storeSlug.Trim().ToLowerInvariant();
                var store = await _context.Stores.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Slug == slug && s.Status == StoreStatus.Verified);
                if (store == null || store.OwnerId == userId) return;
                if (await _context.StoreAttributions.AnyAsync(a => a.UserId == userId)) return;

                _context.StoreAttributions.Add(new StoreAttribution { UserId = userId, StoreId = store.Id, Source = AttributionSource.Qr });
                if (!await _context.Follows.AnyAsync(f => f.FollowerId == userId && f.FollowedStoreId == store.Id))
                    _context.Follows.Add(new Follow { FollowerId = userId, FollowedStoreId = store.Id });
                await _context.SaveChangesAsync();
                _logger.LogInformation("Utente {UserId} registrato dal QR del negozio {StoreId}", userId, store.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Attribuzione al negozio '{Slug}' non riuscita per l'utente {UserId}", storeSlug, userId);
            }
        }

        private async Task SendVerificationEmailAsync(User user)
        {
            if (string.IsNullOrEmpty(user.EmailVerificationToken)) return;
            var link = $"{_emailSettings.FrontendBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(user.EmailVerificationToken)}";
            await TrySendEmailAsync(user, EmailTemplates.VerifyEmail(user.FirstName, link));
        }

        /// <summary>
        /// Un errore del provider email non deve far fallire registrazione o reset: si registra e si prosegue.
        /// </summary>
        private async Task TrySendEmailAsync(User user, EmailMessage message)
        {
            try
            {
                await _emailSender.SendAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), message, HttpContext?.RequestAborted ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invio email '{Subject}' fallito per utente {UserId}", message.Subject, user.Id);
            }
        }

        private static string GenerateUrlSafeToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string HashToken(string token)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash);
        }

        private int MinimumAge(IConfiguration? configuration) =>
            Math.Max(AgeRules.DefaultMinimumAge,
                (configuration ?? HttpContext?.RequestServices?.GetService<IConfiguration>())?.GetValue<int?>("Registration:MinimumAge") ?? AgeRules.DefaultMinimumAge);

        /// <summary>
        /// Data di nascita per gli account creati prima del controllo dell'età. Si imposta una sola volta.
        /// Sotto l'età minima l'account viene sospeso: senza consenso dei genitori non si possono trattare i dati.
        /// </summary>
        [HttpPost("birth-date")]
        [Authorize]
        public async Task<ActionResult<UserDto>> SetBirthDate([FromBody] BirthDateRequest request)
        {
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId)) return Unauthorized();
            var user = await _context.Users.Include(u => u.Location).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Unauthorized();
            if (user.BirthDate != null)
                return Conflict(new { message = "La data di nascita è già stata indicata. Per correggerla contatta il supporto." });

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (request.BirthDate == null || !AgeRules.IsPlausible(request.BirthDate.Value, today))
                return BadRequest(new { message = "Data di nascita non valida" });

            user.BirthDate = request.BirthDate;
            var minimumAge = MinimumAge(null);
            if (!AgeRules.MeetsMinimum(request.BirthDate.Value, today, minimumAge))
            {
                user.IsActive = false;
                user.RefreshToken = null;
                await _context.SaveChangesAsync();
                _logger.LogWarning("Account {UserId} sospeso: età inferiore al minimo di {MinimumAge} anni", userId, minimumAge);
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = $"Per usare CardExchange devi avere almeno {minimumAge} anni. L'account è stato sospeso; per cancellare i tuoi dati scrivi al supporto.",
                    code = "UNDERAGE"
                });
            }

            await _context.SaveChangesAsync();
            return Ok(MapToUserDto(user));
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
                EmailConfirmed = user.EmailConfirmed,
                BirthDateRequired = user.BirthDate == null,
                IsMinor = user.BirthDate != null && AgeRules.IsMinor(user.BirthDate.Value, DateOnly.FromDateTime(DateTime.UtcNow)),
                ReputationScore = user.ReputationScore,
                TotalTradesCompleted = user.TotalTradesCompleted,
                TotalReviewsReceived = user.TotalReviewsReceived,
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

    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = "L'email è obbligatoria")]
        [EmailAddress(ErrorMessage = "Email non valida")]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = "Il token è obbligatorio")]
        [MaxLength(200)]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nuova password è obbligatoria")]
        [MinLength(8, ErrorMessage = "La password deve essere di almeno 8 caratteri")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d\s]).{8,}$",
            ErrorMessage = "La password deve contenere almeno una maiuscola, una minuscola, un numero e un simbolo (es. ! # . - @)")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La conferma password è obbligatoria")]
        [Compare("NewPassword", ErrorMessage = "Le password non coincidono")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    // DTO per cambio password
    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "La password corrente è obbligatoria")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nuova password è obbligatoria")]
        [MinLength(8, ErrorMessage = "La password deve essere di almeno 8 caratteri")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d\s]).{8,}$",
            ErrorMessage = "La password deve contenere almeno una maiuscola, una minuscola, un numero e un simbolo (es. ! # . - @)")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La conferma password è obbligatoria")]
        [Compare("NewPassword", ErrorMessage = "Le password non coincidono")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}