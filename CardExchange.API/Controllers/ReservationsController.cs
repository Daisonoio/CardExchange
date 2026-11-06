using System.Security.Claims;
using CardExchange.API.Services.Stores;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    /// <summary>"Prenota e ritira": carte del negozio, prenotazioni dell'utente e del negozio.</summary>
    [ApiController]
    [Authorize]
    public class ReservationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ReservationService _reservations;

        public ReservationsController(ApplicationDbContext context, ReservationService reservations)
        {
            _context = context;
            _reservations = reservations;
        }

        public record CreateReservationRequest(int CardId, int Quantity = 1);
        public record PickupRequest(string Code);

        /// <summary>Carte disponibili del negozio (quelle in vendita o scambio del proprietario), con la quantità prenotabile.</summary>
        [HttpGet("api/stores/{storeId:int}/cards")]
        [AllowAnonymous]
        public async Task<IActionResult> StoreCards(int storeId, [FromQuery] string? q, [FromQuery] int? gameId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
        {
            var store = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId && s.Status == StoreStatus.Verified);
            if (store == null) return NotFound(new { message = "Negozio non trovato" });
            pageSize = Math.Clamp(pageSize, 1, 60);
            page = Math.Max(1, page);
            var query = _context.Cards.AsNoTracking()
                .Where(c => c.UserId == store.OwnerId && c.IsAvailableForTrade && c.Quantity - c.ReservedQuantity > 0);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(c => c.CardInfo.Name.ToLower().Contains(q.Trim().ToLower()));
            if (gameId.HasValue) query = query.Where(c => c.CardInfo.CardSet.GameId == gameId.Value);
            var total = await query.CountAsync();
            var cards = await query.Include(c => c.CardInfo).ThenInclude(ci => ci.CardSet)
                .OrderBy(c => c.CardInfo.Name).ThenBy(c => c.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var acceptsReservations = await _reservations.StoreAcceptsReservationsAsync(store, DateTime.UtcNow);
            return Ok(new
            {
                total,
                page,
                pageSize,
                acceptsReservations,
                items = cards.Select(c => new
                {
                    c.Id,
                    c.CardInfoId,
                    name = c.CardInfo.Name,
                    setName = c.CardInfo.CardSet?.Name,
                    imageUrl = c.CardInfo.ImageSmall ?? c.CardInfo.ImageNormal ?? c.CardInfo.ImageUrl,
                    condition = c.Condition.ToString(),
                    finish = c.Finish.ToString(),
                    c.Language,
                    available = c.Quantity - c.ReservedQuantity,
                    priceEur = CardPricing.EffectivePriceEur(c)
                })
            });
        }

        [HttpPost("api/stores/{storeId:int}/reservations")]
        public async Task<IActionResult> Create(int storeId, [FromBody] CreateReservationRequest request)
        {
            try
            {
                var r = await _reservations.CreateAsync(storeId, CurrentUserId(), request.CardId, request.Quantity, DateTime.UtcNow);
                this.StoreActivity()?.Record(storeId, StoreActivityType.ReservationCreated, r.UserId, r.Id);
                return Ok(await ToDtoAsync(r.Id));
            }
            catch (ReservationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("api/reservations/mine")]
        public async Task<IActionResult> Mine()
        {
            var me = CurrentUserId();
            var since = DateTime.UtcNow.AddDays(-30);
            var ids = await _context.Reservations.AsNoTracking()
                .Where(r => r.UserId == me && (ReservationService.ActiveStatuses.Contains(r.Status) || r.ClosedAt >= since))
                .OrderByDescending(r => r.CreatedAt).Select(r => r.Id).Take(50).ToListAsync();
            return Ok(await ToDtosAsync(ids, includeUser: false));
        }

        /// <summary>Prenotazioni del negozio: attive (da preparare) e chiuse negli ultimi 30 giorni. Solo il proprietario.</summary>
        [HttpGet("api/stores/{storeId:int}/reservations")]
        public async Task<IActionResult> ForStore(int storeId)
        {
            var store = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId);
            if (store == null) return NotFound(new { message = "Negozio non trovato" });
            if (store.OwnerId != CurrentUserId()) return Forbid();
            var since = DateTime.UtcNow.AddDays(-30);
            var ids = await _context.Reservations.AsNoTracking()
                .Where(r => r.StoreId == storeId && (ReservationService.ActiveStatuses.Contains(r.Status) || r.ClosedAt >= since))
                .OrderBy(r => r.ExpiresAt).Select(r => r.Id).Take(200).ToListAsync();
            return Ok(await ToDtosAsync(ids, includeUser: true));
        }

        [HttpPost("api/reservations/{id:int}/confirm")]
        public Task<IActionResult> Confirm(int id) => Act(id, () => _reservations.ConfirmAsync(id, CurrentUserId(), DateTime.UtcNow));

        [HttpPost("api/reservations/{id:int}/pickup")]
        public Task<IActionResult> Pickup(int id, [FromBody] PickupRequest request) => Act(id, async () =>
        {
            var r = await _reservations.PickUpAsync(id, CurrentUserId(), request.Code, DateTime.UtcNow);
            this.StoreActivity()?.Record(r.StoreId, StoreActivityType.ReservationPickedUp, r.UserId, r.Id);
            return r;
        });

        [HttpPost("api/reservations/{id:int}/cancel")]
        public Task<IActionResult> Cancel(int id) => Act(id, () => _reservations.CancelAsync(id, CurrentUserId(), DateTime.UtcNow));

        [HttpPost("api/reservations/{id:int}/no-show")]
        public Task<IActionResult> NoShow(int id) => Act(id, () => _reservations.NoShowAsync(id, CurrentUserId(), DateTime.UtcNow));

        private async Task<IActionResult> Act(int id, Func<Task<Reservation>> action)
        {
            try
            {
                var r = await action();
                return Ok(await ToDtoAsync(r.Id));
            }
            catch (ReservationException ex)
            {
                return ex.Message == "Prenotazione non trovata" ? NotFound(new { message = ex.Message }) : BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        private async Task<object> ToDtoAsync(int id) => (await ToDtosAsync(new List<int> { id }, includeUser: true)).Single();

        private async Task<List<object>> ToDtosAsync(List<int> ids, bool includeUser)
        {
            var me = CurrentUserId();
            var rows = await _context.Reservations.AsNoTracking().IgnoreQueryFilters()
                .Where(r => ids.Contains(r.Id))
                .Select(r => new
                {
                    r.Id, r.StoreId, StoreName = r.Store.Name, StoreAddress = r.Store.Address, StoreCity = r.Store.City, StoreOwnerId = r.Store.OwnerId,
                    r.CardId, CardName = r.Card.CardInfo.Name, SetName = r.Card.CardInfo.CardSet.Name,
                    ImageUrl = r.Card.CardInfo.ImageSmall ?? r.Card.CardInfo.ImageNormal ?? r.Card.CardInfo.ImageUrl,
                    Condition = r.Card.Condition, r.Card.Language, r.Card.Finish,
                    r.UserId, Username = r.User.Username,
                    r.Quantity, r.PriceEur, r.Status, r.ExpiresAt, r.PickupCode, r.CreatedAt, r.ConfirmedAt, r.ClosedAt
                })
                .ToListAsync();
            return ids.Select(id => rows.First(x => x.Id == id)).Select(r => (object)new
            {
                r.Id, r.StoreId, r.StoreName, r.StoreAddress, r.StoreCity, r.CardId, r.CardName, r.SetName, r.ImageUrl,
                condition = r.Condition.ToString(), r.Language, finish = r.Finish.ToString(),
                r.Quantity, r.PriceEur, status = r.Status.ToString(), r.ExpiresAt, r.CreatedAt, r.ConfirmedAt, r.ClosedAt,
                // Il codice lo vede solo chi ha prenotato; il negozio lo chiede in cassa
                pickupCode = r.UserId == me ? r.PickupCode : null,
                // Il negozio vede chi ha prenotato: è l'utente ad averlo scelto prenotando
                username = includeUser && r.StoreOwnerId == me ? r.Username : null
            }).ToList();
        }

        private int CurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    }
}
