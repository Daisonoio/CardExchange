using System.Security.Claims;
using CardExchange.API.Services.Stores;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    /// <summary>Lista acquisti dei negozi: richieste, proposte degli utenti, consegna in negozio.</summary>
    [ApiController]
    [Authorize]
    public class BuyListController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly BuyListService _buyList;

        public BuyListController(ApplicationDbContext context, BuyListService buyList)
        {
            _context = context;
            _buyList = buyList;
        }

        public record BuyOrderRequest(int CardInfoId, int MinCondition, int? Finish, string? Language, decimal? PriceCashEur, decimal? PriceCreditEur,
            int QuantityWanted, DateTime? ExpiresAt, bool IsActive = true);
        public record ProposeRequest(int CardId, int Quantity = 1);
        public record NoteRequest(string? Note);

        private static BuyOrderInput ToInput(BuyOrderRequest r) => new(r.CardInfoId, (CardCondition)r.MinCondition,
            r.Finish is null or 0 ? null : (CardFinish)r.Finish, r.Language, r.PriceCashEur, r.PriceCreditEur, r.QuantityWanted, r.ExpiresAt, r.IsActive);

        private async Task<(Store? Store, IActionResult? Error)> OwnStoreAsync(int storeId)
        {
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == storeId);
            if (store == null) return (null, NotFound(new { message = "Negozio non trovato" }));
            if (store.OwnerId != CurrentUserId()) return (null, Forbid());
            if (!await _buyList.StoreHasPlanAsync(store, DateTime.UtcNow))
                return (null, StatusCode(402, new { code = "PLAN_REQUIRED", message = "La lista acquisti è inclusa nel piano Negozio Pro" }));
            return (store, null);
        }

        // ---------------- Negozio ----------------

        /// <summary>La lista acquisti del negozio con le proposte ricevute (solo il proprietario).</summary>
        [HttpGet("api/stores/{storeId:int}/buy-orders")]
        public async Task<IActionResult> List(int storeId)
        {
            var (store, error) = await OwnStoreAsync(storeId);
            if (store == null) return error!;
            var orders = await _context.StoreBuyOrders.AsNoTracking().Include(o => o.CardInfo).ThenInclude(ci => ci.CardSet)
                .Where(o => o.StoreId == storeId)
                .OrderByDescending(o => o.IsActive).ThenByDescending(o => o.CreatedAt).ToListAsync();
            var ids = orders.Select(o => o.Id).ToList();
            var since = DateTime.UtcNow.AddDays(-30);
            var offers = await _context.StoreBuyOffers.AsNoTracking().Include(o => o.User).Include(o => o.Card)
                .Where(o => ids.Contains(o.BuyOrderId) && (BuyListService.OpenStatuses.Contains(o.Status) || o.ClosedAt >= since))
                .OrderBy(o => o.CreatedAt).ToListAsync();
            return Ok(orders.Select(o => new
            {
                o.Id, o.CardInfoId, cardName = o.CardInfo.Name, setName = o.CardInfo.CardSet?.Name,
                imageUrl = o.CardInfo.ImageSmall ?? o.CardInfo.ImageNormal ?? o.CardInfo.ImageUrl,
                minCondition = (int)o.MinCondition, finish = o.Finish.HasValue ? (int?)o.Finish : null, o.Language,
                o.PriceCashEur, o.PriceCreditEur, o.QuantityWanted, o.QuantityFilled, o.QuantityPending, o.ExpiresAt, o.IsActive,
                offers = offers.Where(x => x.BuyOrderId == o.Id).Select(x => new
                {
                    x.Id, x.UserId, username = x.User.Username, x.Quantity, status = x.Status.ToString(),
                    condition = (int)x.Card.Condition, x.Card.Language, finish = (int)x.Card.Finish, x.DropOffBy, x.CreatedAt
                })
            }));
        }

        [HttpPost("api/stores/{storeId:int}/buy-orders")]
        public async Task<IActionResult> Create(int storeId, [FromBody] BuyOrderRequest request)
        {
            var (store, error) = await OwnStoreAsync(storeId);
            if (store == null) return error!;
            try
            {
                var o = await _buyList.CreateOrderAsync(store, ToInput(request), DateTime.UtcNow);
                return Ok(new { o.Id });
            }
            catch (BuyListException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("api/stores/{storeId:int}/buy-orders/{orderId:int}")]
        public async Task<IActionResult> Update(int storeId, int orderId, [FromBody] BuyOrderRequest request)
        {
            var (store, error) = await OwnStoreAsync(storeId);
            if (store == null) return error!;
            try
            {
                var o = await _buyList.UpdateOrderAsync(store, orderId, ToInput(request));
                return Ok(new { o.Id });
            }
            catch (BuyListException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("api/buy-offers/{id:int}/accept")]
        public Task<IActionResult> Accept(int id, [FromBody] NoteRequest? body) => Act(() => _buyList.AcceptAsync(id, CurrentUserId(), body?.Note, DateTime.UtcNow));

        [HttpPost("api/buy-offers/{id:int}/reject")]
        public Task<IActionResult> Reject(int id, [FromBody] NoteRequest? body) => Act(() => _buyList.RejectAsync(id, CurrentUserId(), body?.Note, DateTime.UtcNow));

        [HttpPost("api/buy-offers/{id:int}/complete")]
        public Task<IActionResult> Complete(int id) => Act(async () =>
        {
            var offer = await _buyList.CompleteAsync(id, CurrentUserId(), DateTime.UtcNow);
            this.StoreActivity()?.Record(offer.BuyOrder.StoreId, StoreActivityType.BuyOfferCompleted, offer.UserId, offer.Id);
            return offer;
        });

        // ---------------- Utente ----------------

        /// <summary>Richieste di negozi vicini che corrispondono alle carte della mia collezione.</summary>
        [HttpGet("api/buy-orders/for-me")]
        public async Task<IActionResult> ForMe()
        {
            var list = await _buyList.ForMeAsync(CurrentUserId(), DateTime.UtcNow);
            return Ok(list.Select(x => new
            {
                orderId = x.Order.Id, storeId = x.Store.Id, storeName = x.Store.Name, storeCity = x.Store.City, storeOwnerId = x.Store.OwnerId,
                distanceKm = x.DistanceKm,
                cardInfoId = x.Order.CardInfoId, cardName = x.Order.CardInfo.Name, setName = x.Order.CardInfo.CardSet?.Name,
                imageUrl = x.Order.CardInfo.ImageSmall ?? x.Order.CardInfo.ImageNormal ?? x.Order.CardInfo.ImageUrl,
                minCondition = (int)x.Order.MinCondition, finish = x.Order.Finish.HasValue ? (int?)x.Order.Finish : null, language = x.Order.Language,
                x.Order.PriceCashEur, x.Order.PriceCreditEur, remaining = BuyListService.Remaining(x.Order),
                myCards = x.MyCards.Select(c => new { c.Id, condition = (int)c.Condition, c.Language, finish = (int)c.Finish, available = c.Quantity - c.ReservedQuantity }),
                myOffer = x.MyOffer == null ? null : new { x.MyOffer.Id, x.MyOffer.Quantity, status = x.MyOffer.Status.ToString(), x.MyOffer.DropOffBy, x.MyOffer.StoreNote }
            }));
        }

        [HttpPost("api/buy-orders/{orderId:int}/offers")]
        public async Task<IActionResult> Propose(int orderId, [FromBody] ProposeRequest request)
        {
            try
            {
                var o = await _buyList.ProposeAsync(CurrentUserId(), orderId, request.CardId, request.Quantity, DateTime.UtcNow);
                return Ok(new { o.Id, status = o.Status.ToString() });
            }
            catch (BuyListException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("api/buy-offers/{id:int}/withdraw")]
        public Task<IActionResult> Withdraw(int id) => Act(() => _buyList.WithdrawAsync(id, CurrentUserId(), DateTime.UtcNow));

        private async Task<IActionResult> Act(Func<Task<StoreBuyOffer>> action)
        {
            try
            {
                var o = await action();
                return Ok(new { o.Id, status = o.Status.ToString(), o.DropOffBy });
            }
            catch (BuyListException ex)
            {
                return ex.Message == "Proposta non trovata" ? NotFound(new { message = ex.Message }) : BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        private int CurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    }
}
