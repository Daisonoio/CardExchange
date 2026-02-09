using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Gestione account utente e conformità GDPR
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            ApplicationDbContext context,
            IUserRepository userRepository,
            ILogger<AccountController> logger)
        {
            _context = context;
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <summary>
        /// Esporta tutti i dati dell'utente (GDPR Art. 20 - Diritto alla portabilità)
        /// </summary>
        [HttpGet("export-data")]
        public async Task<IActionResult> ExportUserData()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Location)
                .Include(u => u.Cards).ThenInclude(c => c.CardInfo).ThenInclude(ci => ci.CardSet).ThenInclude(cs => cs.Game)
                .Include(u => u.WishlistItems).ThenInclude(w => w.CardInfo)
                .Include(u => u.SentOffers).ThenInclude(o => o.Items).ThenInclude(i => i.Card).ThenInclude(c => c.CardInfo)
                .Include(u => u.ReceivedOffers).ThenInclude(o => o.Items).ThenInclude(i => i.Card).ThenInclude(c => c.CardInfo)
                .Include(u => u.Notifications)
                .Include(u => u.Subscriptions).ThenInclude(s => s.Plan)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound();

            var exportData = new
            {
                exportDate = DateTime.UtcNow,
                format = "GDPR Data Export",
                personalInfo = new
                {
                    user.Email,
                    user.Username,
                    user.FirstName,
                    user.LastName,
                    user.Bio,
                    user.AvatarUrl,
                    user.EmailConfirmed,
                    user.CreatedAt,
                    user.LastLoginAt,
                    user.ReputationScore,
                    user.TotalTradesCompleted,
                    user.TotalReviewsReceived
                },
                location = user.Location != null ? new
                {
                    user.Location.City,
                    user.Location.Province,
                    user.Location.Country,
                    user.Location.PostalCode,
                    user.Location.Latitude,
                    user.Location.Longitude
                } : null,
                cards = user.Cards.Select(c => new
                {
                    c.Id,
                    cardName = c.CardInfo?.Name,
                    cardSet = c.CardInfo?.CardSet?.Name,
                    game = c.CardInfo?.CardSet?.Game?.Name,
                    condition = c.Condition.ToString(),
                    c.Quantity,
                    c.Notes,
                    c.IsAvailableForTrade,
                    c.EstimatedValue,
                    c.CreatedAt
                }),
                wishlist = user.WishlistItems.Select(w => new
                {
                    w.Id,
                    cardName = w.CardInfo?.Name,
                    w.Priority,
                    w.MaxPrice,
                    w.Notes,
                    w.CreatedAt
                }),
                sentOffers = user.SentOffers.Select(o => new
                {
                    o.Id,
                    o.ReceiverId,
                    status = o.Status.ToString(),
                    o.Message,
                    o.CreatedAt,
                    o.ResponseDate,
                    o.CompletedDate,
                    items = o.Items.Select(i => new { i.CardId, cardName = i.Card?.CardInfo?.Name, side = i.Side.ToString(), i.Quantity })
                }),
                receivedOffers = user.ReceivedOffers.Select(o => new
                {
                    o.Id,
                    o.SenderId,
                    status = o.Status.ToString(),
                    o.Message,
                    o.CreatedAt,
                    o.ResponseDate,
                    o.CompletedDate,
                    items = o.Items.Select(i => new { i.CardId, cardName = i.Card?.CardInfo?.Name, side = i.Side.ToString(), i.Quantity })
                }),
                notifications = user.Notifications.Select(n => new
                {
                    n.Id,
                    type = n.Type.ToString(),
                    n.Title,
                    n.Body,
                    n.IsRead,
                    n.CreatedAt
                }),
                subscriptions = user.Subscriptions.Select(s => new
                {
                    planName = s.Plan?.Name,
                    status = s.Status.ToString(),
                    s.StartDate,
                    s.EndDate,
                    s.CreatedAt
                })
            };

            _logger.LogInformation("Esportazione dati GDPR per utente {UserId}", userId);

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"cardexchange-data-{userId}-{DateTime.UtcNow:yyyyMMdd}.json");
        }

        /// <summary>
        /// Elimina l'account e tutti i dati associati (GDPR Art. 17 - Diritto all'oblio)
        /// </summary>
        [HttpDelete("delete-account")]
        public async Task<IActionResult> DeleteAccount([FromQuery] string confirmUsername)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return NotFound();

            // Richiedi conferma tramite username
            if (string.IsNullOrEmpty(confirmUsername) || confirmUsername != user.Username)
                return BadRequest(new { message = "Conferma il tuo username per procedere con l'eliminazione" });

            // Soft-delete di tutti i dati correlati
            var cards = await _context.Cards.Where(c => c.UserId == userId).ToListAsync();
            foreach (var card in cards) card.IsDeleted = true;

            var wishlist = await _context.WishlistItems.Where(w => w.UserId == userId).ToListAsync();
            foreach (var item in wishlist) item.IsDeleted = true;

            var notifications = await _context.Notifications.Where(n => n.UserId == userId).ToListAsync();
            foreach (var n in notifications) n.IsDeleted = true;

            var savedSearches = await _context.SavedSearches.Where(s => s.UserId == userId).ToListAsync();
            foreach (var s in savedSearches) s.IsDeleted = true;

            var subscriptions = await _context.UserSubscriptions.Where(s => s.UserId == userId).ToListAsync();
            foreach (var s in subscriptions) { s.IsDeleted = true; s.Status = SubscriptionStatus.Cancelled; }

            // Anonimizza l'utente (preserva integrità referenziale)
            user.Email = $"deleted_{userId}@removed.local";
            user.Username = $"deleted_user_{userId}";
            user.FirstName = "Utente";
            user.LastName = "Eliminato";
            user.Bio = null;
            user.AvatarUrl = null;
            user.PasswordHash = string.Empty;
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            user.IsActive = false;
            user.IsDeleted = true;

            // Soft delete location
            var location = await _context.UserLocations.FirstOrDefaultAsync(l => l.UserId == userId);
            if (location != null) location.IsDeleted = true;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Account eliminato (GDPR): utente {UserId}", userId);

            return Ok(new { message = "Il tuo account e tutti i dati associati sono stati eliminati. Questa azione è irreversibile." });
        }

        /// <summary>
        /// Ottiene un riepilogo dei dati personali conservati
        /// </summary>
        [HttpGet("privacy-summary")]
        public async Task<IActionResult> GetPrivacySummary()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var cardCount = await _context.Cards.CountAsync(c => c.UserId == userId);
            var wishlistCount = await _context.WishlistItems.CountAsync(w => w.UserId == userId);
            var tradeCount = await _context.TradeOffers.CountAsync(t => t.SenderId == userId || t.ReceiverId == userId);
            var messageCount = await _context.Messages.CountAsync(m => m.SenderId == userId);
            var notificationCount = await _context.Notifications.CountAsync(n => n.UserId == userId);

            return Ok(new
            {
                dataCategories = new
                {
                    personalInfo = "Nome, cognome, email, username, bio",
                    locationData = "Città, provincia, paese, coordinate GPS",
                    cardCollection = $"{cardCount} carte registrate",
                    wishlist = $"{wishlistCount} elementi nella wishlist",
                    trades = $"{tradeCount} offerte di scambio",
                    messages = $"{messageCount} messaggi inviati",
                    notifications = $"{notificationCount} notifiche"
                },
                rights = new
                {
                    export = "GET /api/account/export-data - Scarica tutti i tuoi dati",
                    delete = "DELETE /api/account/delete-account?confirmUsername=tuousername - Elimina il tuo account",
                    modify = "PUT /api/users/{id} - Modifica i tuoi dati personali"
                }
            });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
