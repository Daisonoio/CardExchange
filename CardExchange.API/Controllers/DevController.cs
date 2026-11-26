using CardExchange.API.Services.Seeding;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Strumenti solo per lo sviluppo: fuori dall'ambiente Development rispondono sempre 404.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("api/dev")]
    public class DevController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DevController> _logger;

        public DevController(ApplicationDbContext context, IWebHostEnvironment env, ILogger<DevController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// Crea gli utenti di prova mancanti (docs/utenti-di-test.md) e dice quali ci sono.
        /// Utile se all'avvio non sono stati creati: in caso di errore lo restituisce per intero.
        /// </summary>
        [HttpPost("test-users")]
        public async Task<IActionResult> SeedTestUsers()
        {
            if (!_env.IsDevelopment()) return NotFound();
            try
            {
                var report = await TestUsersSeeder.SeedAsync(_context, _logger);
                var present = await _context.Users.IgnoreQueryFilters()
                    .Where(u => report.Usernames.Contains(u.Username))
                    .Select(u => new { u.Username, u.Email, profileType = u.ProfileType.ToString(), u.IsActive })
                    .ToListAsync();
                return Ok(new
                {
                    created = report.Created,
                    password = TestUsersSeeder.Password,
                    database = _context.Database.GetDbConnection().Database,
                    users = present
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Creazione degli utenti di prova non riuscita");
                return StatusCode(500, new { message = ex.GetBaseException().Message, detail = ex.ToString() });
            }
        }
    }
}
