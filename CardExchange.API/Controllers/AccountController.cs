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

            // Tutti gli altri dati personali, raccolti a parte per non appesantire la query principale
            var messagesSent = await _context.Messages.AsNoTracking()
                .Where(m => m.SenderId == userId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new { m.ConversationId, m.Content, m.CreatedAt, m.IsRead })
                .ToListAsync();
            var conversations = await _context.Conversations.AsNoTracking()
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .Select(c => new { c.Id, withUser = c.User1Id == userId ? c.User2.Username : c.User1.Username, c.TradeOfferId, c.CreatedAt, c.LastMessageAt })
                .ToListAsync();
            var reviewsWritten = await _context.TradeReviews.AsNoTracking()
                .Where(r => r.ReviewerId == userId)
                .Select(r => new { r.TradeOfferId, reviewedUser = r.ReviewedUser.Username, r.Rating, r.Comment, r.CreatedAt })
                .ToListAsync();
            var reviewsReceived = await _context.TradeReviews.AsNoTracking()
                .Where(r => r.ReviewedUserId == userId)
                .Select(r => new { r.TradeOfferId, r.Rating, r.Comment, r.CreatedAt })
                .ToListAsync();
            var meetups = await _context.TradeMeetups.AsNoTracking()
                .Where(m => m.TradeOffer.SenderId == userId || m.TradeOffer.ReceiverId == userId)
                .Select(m => new { m.TradeOfferId, m.ScheduledAt, m.LocationName, m.Address, status = m.Status.ToString(), proposedByMe = m.ProposedById == userId, m.Notes })
                .ToListAsync();
            var reportsFiled = await _context.UserReports.AsNoTracking()
                .Where(r => r.ReporterId == userId)
                .Select(r => new { reportedUser = r.ReportedUser.Username, reason = r.Reason.ToString(), r.Details, status = r.Status.ToString(), r.CreatedAt })
                .ToListAsync();
            var blocked = await _context.UserBlocks.AsNoTracking()
                .Where(b => b.BlockerId == userId)
                .Select(b => new { username = b.BlockedUser.Username, b.CreatedAt })
                .ToListAsync();
            var following = await _context.Follows.AsNoTracking()
                .Where(f => f.FollowerId == userId)
                .Select(f => new { user = f.FollowedUser != null ? f.FollowedUser.Username : null, store = f.FollowedStore != null ? f.FollowedStore.Name : null, f.CreatedAt })
                .ToListAsync();
            var followersCount = await _context.Follows.CountAsync(f => f.FollowedUserId == userId);
            var broadcasts = await _context.LocationBroadcasts.AsNoTracking()
                .Where(b => b.AuthorId == userId)
                .Select(b => new { b.PlaceName, b.SpotDetail, b.Message, b.Latitude, b.Longitude, b.StartsAt, b.EndsAt, b.RecipientCount })
                .ToListAsync();
            var storeOfOrigin = await _context.StoreAttributions.AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => new { store = a.Store.Name, a.CreatedAt })
                .FirstOrDefaultAsync();
            var storesOwned = await _context.Stores.AsNoTracking()
                .Where(st => st.OwnerId == userId)
                .Select(st => new { st.Name, st.Address, st.City, st.Phone, st.Website, st.Instagram, status = st.Status.ToString(), st.CreatedAt })
                .ToListAsync();
            var eventsOrganized = await _context.Events.AsNoTracking()
                .Where(e => e.OrganizerId == userId)
                .Select(e => new { e.Title, e.StartDate, e.City, status = e.Status.ToString() })
                .ToListAsync();
            var eventsJoined = await _context.EventParticipants.AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => new { p.Event.Title, p.Event.StartDate, status = p.Status.ToString(), p.CheckedInAt })
                .ToListAsync();
            var favorites = await _context.FavoriteCards.AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => new { f.CardId, cardName = f.Card.CardInfo.Name, f.CreatedAt })
                .ToListAsync();
            var priceAlerts = await _context.PriceAlerts.AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => new { cardName = a.CardInfo.Name, a.TargetPrice, direction = a.Direction.ToString(), a.IsActive, a.TriggeredAt, a.CreatedAt })
                .ToListAsync();
            var savedSearches = await _context.SavedSearches.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new { x.Name, x.CriteriaJson, x.AlertEnabled, x.CreatedAt })
                .ToListAsync();
            var devices = await _context.PushSubscriptions.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new { x.UserAgent, x.CreatedAt, x.LastSuccessAt })
                .ToListAsync();
            var notificationPreferences = await _context.NotificationPreferences.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new { type = x.Type.ToString(), x.IsEnabled })
                .ToListAsync();
            var loyaltyCards = await _context.LoyaltyCards.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new { store = x.Store.Name, x.Stamps, x.RewardsRedeemed, x.LastStampAt, x.CreatedAt })
                .ToListAsync();
            var invitedBy = user.ReferredByUserId == null ? null
                : await _context.Users.AsNoTracking().Where(u => u.Id == user.ReferredByUserId).Select(u => u.Username).FirstOrDefaultAsync();

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
                    user.TotalReviewsReceived,
                    user.BirthDate,
                    user.TermsAcceptedAt,
                    user.TermsVersion,
                    user.ReferralCode,
                    invitedBy,
                    storeOfOrigin
                },
                loyaltyCards,
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
                    c.AskingPrice,
                    c.Language,
                    finish = c.Finish.ToString(),
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
                }),
                conversations,
                messagesSent,
                reviewsWritten,
                reviewsReceived,
                meetups,
                reportsFiled,
                blockedUsers = blocked,
                following,
                followersCount,
                imHereUpdates = broadcasts,
                storesOwned,
                eventsOrganized,
                eventsJoined,
                favoriteCards = favorites,
                priceAlerts,
                savedSearches,
                pushDevices = devices,
                notificationPreferences
            };

            _logger.LogInformation("Esportazione dati GDPR per utente {UserId}", userId);

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"cardexchange-data-{userId}-{DateTime.UtcNow:yyyyMMdd}.json");
        }

        /// <summary>
        /// Elimina l'account e tutti i dati associati (GDPR Art. 17 - Diritto all'oblio)
        /// Password123!
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

            // "Segui" in entrambe le direzioni (anche verso i suoi negozi) e avvisi "Sono qui": via del tutto
            var ownStoreIds = await _context.Stores.Where(s => s.OwnerId == userId).Select(s => s.Id).ToListAsync();
            _context.Follows.RemoveRange(await _context.Follows
                .Where(f => f.FollowerId == userId || f.FollowedUserId == userId || (f.FollowedStoreId != null && ownStoreIds.Contains(f.FollowedStoreId.Value)))
                .ToListAsync());
            _context.StoreAttributions.RemoveRange(await _context.StoreAttributions.Where(a => a.UserId == userId).ToListAsync());
            var broadcasts = await _context.LocationBroadcasts.Where(b => b.AuthorId == userId).ToListAsync();
            foreach (var b in broadcasts) { b.IsDeleted = true; b.EndedAt ??= DateTime.UtcNow; }

            // Prenotazioni in negozio ancora attive: annullate, le copie tornano disponibili
            var activeReservations = await _context.Reservations
                .Where(r => r.UserId == userId && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)).ToListAsync();
            foreach (var r in activeReservations)
            {
                r.Status = ReservationStatus.Cancelled;
                r.ClosedAt = DateTime.UtcNow;
                var reservedCard = await _context.Cards.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == r.CardId);
                if (reservedCard != null) reservedCard.ReservedQuantity = Math.Max(0, reservedCard.ReservedQuantity - r.Quantity);
            }

            // Proposte alla lista acquisti ancora aperte: ritirate, le copie accettate tornano disponibili al negozio
            var openBuyOffers = await _context.StoreBuyOffers.Include(o => o.BuyOrder)
                .Where(o => o.UserId == userId && (o.Status == BuyOfferStatus.Proposed || o.Status == BuyOfferStatus.Accepted)).ToListAsync();
            foreach (var o in openBuyOffers)
            {
                if (o.Status == BuyOfferStatus.Accepted) o.BuyOrder.QuantityPending = Math.Max(0, o.BuyOrder.QuantityPending - o.Quantity);
                o.Status = BuyOfferStatus.Withdrawn;
                o.ClosedAt = DateTime.UtcNow;
            }

            // Promozioni non ancora consegnate: annullate
            foreach (var p in await _context.Promotions.Where(p => p.OwnerUserId == userId
                         && (p.Status == PromotionStatus.Active || p.Status == PromotionStatus.PendingPayment)).ToListAsync())
                p.Status = PromotionStatus.Cancelled;
            _context.PromotionDeliveries.RemoveRange(await _context.PromotionDeliveries.Where(d => d.UserId == userId).ToListAsync());

            // Tessere fedeltà: via del tutto (i timbri seguono la tessera)
            _context.LoyaltyCards.RemoveRange(await _context.LoyaltyCards.Where(c => c.UserId == userId).ToListAsync());

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

        public record LocaleRequest(string? TimeZone, string? Language);

        /// <summary>
        /// Fuso orario e lingua del dispositivo (rilevati dal browser): orari e testi delle notifiche ed email
        /// arrivano nel fuso e nella lingua dell'utente.
        /// </summary>
        [HttpPut("locale")]
        public async Task<IActionResult> SetLocale([FromBody] LocaleRequest request)
        {
            if (request.TimeZone != null && !CardExchange.Core.Rules.TimeZones.IsValid(request.TimeZone))
                return BadRequest(new { message = "Fuso orario non valido" });
            if (request.Language != null && !CardExchange.API.Localization.ServerText.Supported.Contains(request.Language))
                return BadRequest(new { message = "Lingua non supportata" });
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == GetCurrentUserId());
            if (user == null) return NotFound();
            var changed = false;
            if (request.TimeZone != null && user.TimeZone != request.TimeZone) { user.TimeZone = request.TimeZone; changed = true; }
            if (request.Language != null && user.Language != request.Language) { user.Language = request.Language; changed = true; }
            if (changed) await _context.SaveChangesAsync();
            return Ok(new { timeZone = user.TimeZone, language = user.Language });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
