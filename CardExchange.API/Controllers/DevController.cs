#if DEBUG
using BCrypt.Net;
using CardExchange.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/dev")]
    [AllowAnonymous] // SOLO PER DEBUG!
    public class DevController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<DevController> _logger;

        public DevController(IUserRepository userRepository, ILogger<DevController> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <summary>
        /// SOLO DEBUG - Reset password utente
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDevRequest request)
        {
            try
            {
                var user = await _userRepository.GetByUsernameAsync(request.Username);

                if (user == null)
                {
                    return NotFound(new { message = "Utente non trovato" });
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogWarning("Password resettata per utente {Username} via endpoint DEV", request.Username);

                return Ok(new
                {
                    message = "Password aggiornata con successo",
                    username = user.Username,
                    newPassword = request.NewPassword
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il reset password");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }
    }

    public class ResetPasswordDevRequest
    {
        public string Username { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
#endif