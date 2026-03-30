using CardExchange.API.DTOs.Requests;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Assegna un ruolo a un utente (solo Admin/SuperAdmin)
        /// </summary>
        [HttpPost("assign-role")]
        public async Task<ActionResult> AssignRole([FromBody] AssignRoleRequest request)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                // Verifica che l'utente target esista
                var targetUser = await _context.Users.FindAsync(request.UserId);
                if (targetUser == null)
                {
                    return NotFound(new { message = "Utente non trovato" });
                }

                // Verifica che il ruolo esista
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName);
                if (role == null)
                {
                    return BadRequest(new { message = $"Ruolo '{request.RoleName}' non trovato" });
                }

                // Solo SuperAdmin può assegnare il ruolo SuperAdmin
                if (request.RoleName == "SuperAdmin" && !User.IsInRole("SuperAdmin"))
                {
                    return Forbid();
                }

                // Verifica che l'utente non abbia già il ruolo
                var existingRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == request.UserId && ur.RoleId == role.Id && !ur.IsDeleted);

                if (existingRole != null)
                {
                    return BadRequest(new { message = $"L'utente ha già il ruolo '{request.RoleName}'" });
                }

                var userRole = new UserRole
                {
                    UserId = request.UserId,
                    RoleId = role.Id,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = currentUserId
                };

                await _context.UserRoles.AddAsync(userRole);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Ruolo '{Role}' assegnato all'utente {TargetUserId} da {AdminUserId}",
                    request.RoleName, request.UserId, currentUserId);

                return Ok(new { message = $"Ruolo '{request.RoleName}' assegnato con successo all'utente {targetUser.Username}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'assegnazione del ruolo");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Rimuove un ruolo da un utente (solo Admin/SuperAdmin)
        /// </summary>
        [HttpPost("remove-role")]
        public async Task<ActionResult> RemoveRole([FromBody] RemoveRoleRequest request)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                // Non permettere di rimuovere il proprio ruolo Admin/SuperAdmin
                if (request.UserId == currentUserId && (request.RoleName == "Admin" || request.RoleName == "SuperAdmin"))
                {
                    return BadRequest(new { message = "Non puoi rimuovere il tuo stesso ruolo di amministratore" });
                }

                // Solo SuperAdmin può rimuovere il ruolo SuperAdmin
                if (request.RoleName == "SuperAdmin" && !User.IsInRole("SuperAdmin"))
                {
                    return Forbid();
                }

                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName);
                if (role == null)
                {
                    return BadRequest(new { message = $"Ruolo '{request.RoleName}' non trovato" });
                }

                var userRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == request.UserId && ur.RoleId == role.Id && !ur.IsDeleted);

                if (userRole == null)
                {
                    return NotFound(new { message = $"L'utente non ha il ruolo '{request.RoleName}'" });
                }

                userRole.IsDeleted = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Ruolo '{Role}' rimosso dall'utente {TargetUserId} da {AdminUserId}",
                    request.RoleName, request.UserId, currentUserId);

                return Ok(new { message = $"Ruolo '{request.RoleName}' rimosso con successo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la rimozione del ruolo");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene i ruoli di un utente (solo Admin/SuperAdmin)
        /// </summary>
        [HttpGet("user-roles/{userId}")]
        public async Task<ActionResult> GetUserRoles(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Utente non trovato" });
            }

            var roles = await _context.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == userId && !ur.IsDeleted)
                .Select(ur => new
                {
                    ur.Role.Name,
                    ur.Role.Description,
                    ur.AssignedAt,
                    AssignedBy = ur.AssignedByUser != null ? ur.AssignedByUser.Username : "Sistema"
                })
                .ToListAsync();

            return Ok(new { userId, username = user.Username, roles });
        }

        /// <summary>
        /// Elenca tutti i ruoli disponibili (solo Admin/SuperAdmin)
        /// </summary>
        [HttpGet("roles")]
        public async Task<ActionResult> GetAllRoles()
        {
            var roles = await _context.Roles
                .Select(r => new { r.Id, r.Name, r.Description })
                .ToListAsync();

            return Ok(roles);
        }
    }
}
