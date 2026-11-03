using CardExchange.API.Services.Push;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using PushSubscriptionEntity = CardExchange.Core.Entities.PushSubscription;

namespace CardExchange.API.Controllers
{
    /// <summary>Registrazione dei dispositivi per le notifiche push (Web Push / PWA).</summary>
    [ApiController]
    [Route("api/push")]
    [Authorize]
    public class PushController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly WebPushOptions _options;
        private readonly IPushQueue _queue;

        public PushController(ApplicationDbContext context, IOptions<WebPushOptions> options, IPushQueue queue)
        {
            _context = context;
            _options = options.Value;
            _queue = queue;
        }

        /// <summary>Chiave pubblica VAPID da passare a pushManager.subscribe. enabled=false se il server non è configurato.</summary>
        [HttpGet("public-key")]
        [AllowAnonymous]
        public IActionResult GetPublicKey() =>
            Ok(new { enabled = _options.IsConfigured, publicKey = _options.IsConfigured ? _options.PublicKey : null });

        public class SubscribeRequest
        {
            [Required, MaxLength(1000), Url]
            public string Endpoint { get; set; } = string.Empty;

            [Required]
            public KeysDto Keys { get; set; } = new();

            public class KeysDto
            {
                [Required, MaxLength(200)] public string P256dh { get; set; } = string.Empty;
                [Required, MaxLength(100)] public string Auth { get; set; } = string.Empty;
            }
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();
            if (!_options.IsConfigured) return BadRequest(new { message = "Notifiche push non disponibili su questo server" });
            if (!request.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Endpoint non valido" });

            var existing = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint);
            if (existing == null)
            {
                _context.PushSubscriptions.Add(new PushSubscriptionEntity
                {
                    UserId = userId,
                    Endpoint = request.Endpoint,
                    P256dh = request.Keys.P256dh,
                    Auth = request.Keys.Auth,
                    UserAgent = Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua[..Math.Min(ua.Length, 300)] : null
                });
            }
            else
            {
                // Stesso browser, magari un altro account: la sottoscrizione passa all'utente attuale
                existing.UserId = userId;
                existing.P256dh = request.Keys.P256dh;
                existing.Auth = request.Keys.Auth;
            }
            await _context.SaveChangesAsync();
            return Ok(new { message = "Notifiche attivate su questo dispositivo" });
        }

        public class UnsubscribeRequest
        {
            [Required, MaxLength(1000)]
            public string Endpoint { get; set; } = string.Empty;
        }

        [HttpPost("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
        {
            var userId = CurrentUserId();
            var sub = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint && s.UserId == userId);
            if (sub != null)
            {
                _context.PushSubscriptions.Remove(sub);
                await _context.SaveChangesAsync();
            }
            return Ok(new { message = "Notifiche disattivate su questo dispositivo" });
        }

        /// <summary>Invia una notifica di prova ai dispositivi dell'utente.</summary>
        [HttpPost("test")]
        public IActionResult Test()
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();
            _queue.Enqueue(new PushJob(userId, "CardExchange", "Le notifiche funzionano su questo dispositivo.", "/home", "test"));
            return Accepted(new { message = "Notifica di prova inviata" });
        }

        private int CurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    }
}
