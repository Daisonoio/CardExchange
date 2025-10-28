using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserRepository userRepository, ILogger<UsersController> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene tutti gli utenti, Endpoint solo per ADMIN
        /// </summary> 
        [HttpGet]
        [RequirePermission("USERS.READ.ALL")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
        {
            try
            {
                // TODO: Implementare paginazione per performance
                var users = await _userRepository.GetAllUsersAsync();
                var userDtos = users.Select(MapToDto);
                return Ok(userDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero degli utenti");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene un utente per ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetUserById(int id)
        {
            try
            {
                var user = await _userRepository.GetWithLocationAsync(id);

                if (user == null)
                {
                    return NotFound(new { message = $"Utente con ID {id} non trovato" });
                }

                return Ok(MapToDto(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dell'utente {UserId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene un utente per username
        /// </summary>
        [HttpGet("by-username/{username}")]
        [RequirePermission("USERS.READ.PUBLIC")]
        public async Task<ActionResult<UserDto>> GetUserByUsername(string username)
        {
            try
            {
                var user = await _userRepository.GetByUsernameAsync(username);

                if (user == null)
                {
                    return NotFound(new { message = $"Utente '{username}' non trovato" });
                }

                var userWithLocation = await _userRepository.GetWithLocationAsync(user.Id);
                return Ok(MapToDto(userWithLocation!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dell'utente {Username}", username);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Cerca utenti per località
        /// </summary>
        [HttpGet("by-location")]
        [RequirePermission("USERS.READ.PUBLIC")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsersByLocation(
            [FromQuery] string city,
            [FromQuery] string province,
            [FromQuery] string country)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(country))
                {
                    return BadRequest(new { message = "City, province e country sono obbligatori" });
                }

                var users = await _userRepository.GetUsersByLocationAsync(city, province, country);
                var userDtos = users.Select(MapToDto);

                return Ok(userDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la ricerca utenti per località");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Cerca utenti in un raggio specificato
        /// </summary>
        [HttpGet("nearby")]
        [RequirePermission("SEARCH.GEOGRAPHIC")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetNearbyUsers(
            [FromQuery] decimal latitude,
            [FromQuery] decimal longitude,
            [FromQuery] int radiusKm = 50)
        {
            try
            {
                if (radiusKm < 1 || radiusKm > 1000)
                {
                    return BadRequest(new { message = "Il raggio deve essere tra 1 e 1000 km" });
                }

                var users = await _userRepository.GetUsersInRadiusAsync(latitude, longitude, radiusKm);
                var userDtos = users.Select(MapToDto);

                return Ok(new
                {
                    searchCenter = new { latitude, longitude, radiusKm },
                    count = userDtos.Count(),
                    users = userDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la ricerca utenti nelle vicinanze");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Crea un nuovo utente
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                // Validazione email e username univoci
                if (await _userRepository.EmailExistsAsync(request.Email))
                {
                    return BadRequest(new { message = "Email già in uso" });
                }

                if (await _userRepository.UsernameExistsAsync(request.Username))
                {
                    return BadRequest(new { message = "Username già in uso" });
                }

                // TODO: Implementare hashing password sicuro (BCrypt)
                var passwordHash = HashPassword(request.Password);

                var user = new User
                {
                    Email = request.Email,
                    Username = request.Username,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Bio = request.Bio,
                    PasswordHash = passwordHash,
                    IsActive = true,
                    EmailConfirmed = false
                };

                await _userRepository.CreateAsync(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("Utente creato: {UserId} - {Username}", user.Id, user.Username);

                return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, MapToDto(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la creazione dell'utente");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiorna un utente esistente
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission("USERS.UPDATE.OWN", "USERS.UPDATE.ANY")]
        public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);

                if (user == null)
                {
                    return NotFound(new { message = $"Utente con ID {id} non trovato" });
                }

                // Aggiorna solo i campi forniti
                if (!string.IsNullOrWhiteSpace(request.FirstName))
                    user.FirstName = request.FirstName;

                if (!string.IsNullOrWhiteSpace(request.LastName))
                    user.LastName = request.LastName;

                if (request.Bio != null)
                    user.Bio = request.Bio;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("Utente aggiornato: {UserId}", id);

                var updatedUser = await _userRepository.GetWithLocationAsync(id);
                return Ok(MapToDto(updatedUser!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiornamento dell'utente {UserId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiunge o aggiorna la posizione di un utente
        /// </summary>
        [HttpPut("{id}/location")]
        [RequirePermission("USERS.UPDATE.OWN", "USERS.UPDATE.ANY")]
        public async Task<ActionResult<UserDto>> UpdateUserLocation(int id, [FromBody] CreateUserLocationRequest request)
        {
            try
            {
                var user = await _userRepository.GetWithLocationAsync(id);

                if (user == null)
                {
                    return NotFound(new { message = $"Utente con ID {id} non trovato" });
                }

                if (user.Location == null)
                {
                    // Crea nuova location
                    user.Location = new UserLocation
                    {
                        UserId = id,
                        City = request.City,
                        Province = request.Province,
                        Country = request.Country,
                        PostalCode = request.PostalCode,
                        Latitude = request.Latitude,
                        Longitude = request.Longitude,
                        MaxDistanceKm = request.MaxDistanceKm
                    };
                }
                else
                {
                    // Aggiorna location esistente
                    user.Location.City = request.City;
                    user.Location.Province = request.Province;
                    user.Location.Country = request.Country;
                    user.Location.PostalCode = request.PostalCode;
                    user.Location.Latitude = request.Latitude;
                    user.Location.Longitude = request.Longitude;
                    user.Location.MaxDistanceKm = request.MaxDistanceKm;
                }

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("Location aggiornata per utente: {UserId}", id);

                var updatedUser = await _userRepository.GetWithLocationAsync(id);
                return Ok(MapToDto(updatedUser!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiornamento della location per l'utente {UserId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Disattiva un utente (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission("USERS.DELETE.ANY")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);

                if (user == null)
                {
                    return NotFound(new { message = $"Utente con ID {id} non trovato" });
                }

                _userRepository.Delete(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("Utente disattivato: {UserId}", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la disattivazione dell'utente {UserId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        // Helper methods
        private static UserDto MapToDto(User user)
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

        private static string HashPassword(string password)
        {
            // TODO: Implementare hashing sicuro con BCrypt o Identity
            // Questo è solo un placeholder temporaneo
            return $"HASHED_{password}";
        }
    }
}