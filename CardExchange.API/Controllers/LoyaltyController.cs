using System.Security.Claims;
using CardExchange.API.Services.Stores;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    /// <summary>Tessera fedeltà dei negozi: configurazione, timbri e premi con il QR del cliente, le mie tessere.</summary>
    [ApiController]
    [Authorize]
    public class LoyaltyController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly LoyaltyService _loyalty;

        public LoyaltyController(ApplicationDbContext context, LoyaltyService loyalty)
        {
            _context = context;
            _loyalty = loyalty;
        }

        public record ProgramRequest(int StampsRequired, string RewardDescription, bool IsActive = true);
        public record PassRequest(string Token);

        private int CurrentUserId() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private IActionResult Fail(LoyaltyException ex) => StatusCode(ex.Status, new { message = ex.Message, code = ex.Code });

        private async Task<(Store? Store, IActionResult? Error)> OwnStoreAsync(int storeId)
        {
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == storeId);
            if (store == null) return (null, NotFound(new { message = "Negozio non trovato" }));
            if (store.OwnerId != CurrentUserId()) return (null, Forbid());
            return (store, null);
        }

        /// <summary>Il programma del negozio (pubblico); il proprietario vede anche il piano e i dati aggregati.</summary>
        [HttpGet("api/stores/{storeId:int}/loyalty")]
        public async Task<IActionResult> Get(int storeId)
        {
            var store = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId);
            if (store == null) return NotFound(new { message = "Negozio non trovato" });
            var program = await _context.LoyaltyPrograms.AsNoTracking().FirstOrDefaultAsync(p => p.StoreId == storeId);
            var isOwner = store.OwnerId == CurrentUserId();
            var hasPlan = await _loyalty.StoreHasPlanAsync(store);
            if (!isOwner)
            {
                if (program == null || !program.IsActive || !hasPlan || store.Status != StoreStatus.Verified) return Ok(new { active = false });
                var mine = await _context.LoyaltyCards.AsNoTracking().FirstOrDefaultAsync(c => c.StoreId == storeId && c.UserId == CurrentUserId());
                return Ok(new { active = true, program.StampsRequired, program.RewardDescription, myStamps = mine?.Stamps ?? 0 });
            }
            var stats = program == null ? null : await _loyalty.StatsAsync(storeId, program.StampsRequired, DateTime.UtcNow);
            return Ok(new
            {
                active = program?.IsActive == true && hasPlan,
                hasPlan,
                configured = program != null,
                stampsRequired = program?.StampsRequired ?? 5,
                rewardDescription = program?.RewardDescription ?? "",
                isActive = program?.IsActive ?? true,
                stats
            });
        }

        [HttpPut("api/stores/{storeId:int}/loyalty")]
        public async Task<IActionResult> Configure(int storeId, [FromBody] ProgramRequest request)
        {
            var (store, error) = await OwnStoreAsync(storeId);
            if (store == null) return error!;
            try
            {
                var p = await _loyalty.ConfigureAsync(store, request.StampsRequired, request.RewardDescription, request.IsActive);
                return Ok(new { message = "Tessera fedeltà salvata", p.StampsRequired, p.RewardDescription, p.IsActive });
            }
            catch (LoyaltyException ex) { return Fail(ex); }
        }

        /// <summary>Il personale scansiona il QR del cliente e assegna il timbro del giorno.</summary>
        [HttpPost("api/stores/{storeId:int}/loyalty/stamp")]
        public async Task<IActionResult> Stamp(int storeId, [FromBody] PassRequest request)
        {
            var (store, error) = await OwnStoreAsync(storeId);
            if (store == null) return error!;
            try
            {
                var r = await _loyalty.StampWithPassAsync(store, request.Token, DateTime.UtcNow);
                return Ok(new
                {
                    message = r.Outcome == StampOutcome.AlreadyToday ? "Timbro di oggi già assegnato" : "Timbro assegnato",
                    stamped = r.Outcome == StampOutcome.Stamped,
                    r.Username, r.Stamps, r.StampsRequired, r.RewardReady
                });
            }
            catch (LoyaltyException ex) { return Fail(ex); }
        }

        /// <summary>Riscatto del premio con il QR del cliente.</summary>
        [HttpPost("api/stores/{storeId:int}/loyalty/redeem")]
        public async Task<IActionResult> Redeem(int storeId, [FromBody] PassRequest request)
        {
            var (store, error) = await OwnStoreAsync(storeId);
            if (store == null) return error!;
            try
            {
                var r = await _loyalty.RedeemAsync(store, request.Token, DateTime.UtcNow);
                return Ok(new { message = "Premio consegnato", r.Username, r.Stamps, r.StampsRequired, r.RewardReady });
            }
            catch (LoyaltyException ex) { return Fail(ex); }
        }

        /// <summary>Le mie tessere, con i negozi che hanno la tessera attiva.</summary>
        [HttpGet("api/me/loyalty")]
        public async Task<IActionResult> Mine()
        {
            var userId = CurrentUserId();
            var cards = await _context.LoyaltyCards.AsNoTracking().Include(c => c.Store)
                .Where(c => c.UserId == userId)
                .Join(_context.LoyaltyPrograms.AsNoTracking(), c => c.StoreId, p => p.StoreId, (c, p) => new { c, p })
                .Where(x => x.p.IsActive && x.c.Store.Status == StoreStatus.Verified)
                .OrderByDescending(x => x.c.LastStampAt)
                .ToListAsync();
            return Ok(cards.Select(x => new
            {
                storeId = x.c.StoreId,
                storeName = x.c.Store.Name,
                storeSlug = x.c.Store.Slug,
                city = x.c.Store.City,
                stamps = x.c.Stamps,
                stampsRequired = x.p.StampsRequired,
                reward = x.p.RewardDescription,
                rewardReady = x.c.Stamps >= x.p.StampsRequired,
                rewardsRedeemed = x.c.RewardsRedeemed,
                lastStampAt = x.c.LastStampAt
            }));
        }

        /// <summary>Codice personale da mostrare in negozio: vale 2 minuti, poi se ne genera un altro.</summary>
        [HttpGet("api/me/loyalty/pass")]
        public IActionResult Pass()
        {
            var now = DateTime.UtcNow;
            return Ok(new { token = _loyalty.CreatePass(CurrentUserId(), now), expiresAt = now + LoyaltyPass.Lifetime });
        }
    }
}
