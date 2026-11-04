using CardExchange.API.Authorization;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StatisticsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StatisticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Statistiche base della collezione (gratis): quantità e valore di mercato attuale.
        /// </summary>
        [HttpGet("collection/basic")]
        public async Task<IActionResult> GetBasicStats()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var cards = await LoadCardsAsync(userId);
            var wishlistItems = await _context.WishlistItems.CountAsync(w => w.UserId == userId);
            var pendingTrades = await _context.TradeOffers.CountAsync(t =>
                (t.SenderId == userId || t.ReceiverId == userId) && t.Status == TradeOfferStatus.Pending);

            return Ok(new
            {
                totalCards = cards.Sum(c => c.Quantity),
                uniqueCards = cards.Count,
                availableForTrade = cards.Where(c => c.IsAvailableForTrade).Sum(c => c.Quantity),
                marketValue = cards.Sum(LineValue),
                wishlistItems,
                pendingTrades
            });
        }

        /// <summary>
        /// Statistiche complete (Premium): andamento del valore nel tempo, variazioni, carte di maggior valore,
        /// carte che si sono mosse di più, ripartizione per gioco e condizione. Valori di mercato di riferimento.
        /// </summary>
        [HttpGet("collection/insights")]
        [RequirePremium]
        public async Task<IActionResult> GetInsights([FromQuery] int days = 90)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();
            days = Math.Clamp(days, 7, 365);

            var cards = await LoadCardsAsync(userId);
            var today = DateTime.UtcNow.Date;
            var seriesDays = Math.Max(days, 30);
            var from = today.AddDays(-seriesDays);
            var cardInfoIds = cards.Select(c => c.CardInfoId).Distinct().ToList();

            // Un mese in più per avere l'ultimo prezzo noto prima dell'inizio del periodo
            var historyFrom = from.AddDays(-30);
            var history = cardInfoIds.Count == 0
                ? new List<PricePoint>()
                : (await _context.PriceHistories.AsNoTracking()
                    .Where(h => cardInfoIds.Contains(h.CardInfoId) && h.SnapshotDate >= historyFrom)
                    .Select(h => new { h.CardInfoId, h.SnapshotDate, h.PriceEur, h.PriceEurFoil })
                    .ToListAsync())
                  .Select(h => new PricePoint(h.CardInfoId, h.SnapshotDate, h.PriceEur, h.PriceEurFoil))
                  .ToList();

            // Il prezzo di oggi è quello del catalogo, anche se lo storico notturno non è ancora passato
            history.AddRange(cards
                .GroupBy(c => c.CardInfoId)
                .Select(g => g.First().CardInfo)
                .Where(ci => ci.PriceEur.HasValue || ci.PriceEurFoil.HasValue)
                .Select(ci => new PricePoint(ci.Id, today, ci.PriceEur, ci.PriceEurFoil)));
            history = history
                .GroupBy(h => (h.CardInfoId, h.Date.Date))
                .Select(g => g.Last())
                .ToList();

            var holdings = cards.Select(c => new HoldingLine(c.CardInfoId, c.Finish, c.Condition, c.Quantity)).ToList();
            var series = CollectionValuation.Series(holdings, history, from, today);
            decimal ValueAt(DateTime d) => series.FirstOrDefault(p => p.Date == d)?.Value ?? 0;
            var current = series.Count > 0 ? series[^1].Value : 0;

            object Change(int back)
            {
                var past = ValueAt(today.AddDays(-back));
                var diff = current - past;
                return new { amount = diff, percent = past > 0 ? Math.Round(diff / past * 100, 1) : (decimal?)null };
            }

            // Variazione a 7 giorni del prezzo unitario, pesata per le copie possedute
            var weekAgo = today.AddDays(-7);
            var byCardHistory = history.GroupBy(h => h.CardInfoId).ToDictionary(g => g.Key, g => g.OrderBy(h => h.Date).ToList());
            var movers = cards
                .Select(c =>
                {
                    if (!byCardHistory.TryGetValue(c.CardInfoId, out var pts) || pts.Count < 2) return null;
                    var before = pts.LastOrDefault(p => p.Date.Date <= weekAgo) ?? pts[0];
                    var oldUnit = CollectionValuation.UnitPrice(before, c.Finish);
                    var newUnit = CollectionValuation.UnitPrice(pts[^1], c.Finish);
                    if (oldUnit is not > 0 || newUnit == null || before.Date.Date >= pts[^1].Date.Date) return null;
                    var factor = CardPricing.ConditionFactor(c.Condition);
                    var change = Math.Round((newUnit.Value - oldUnit.Value) * factor, 2);
                    if (change == 0) return null;
                    return new
                    {
                        cardId = c.Id,
                        cardInfoId = c.CardInfoId,
                        name = c.CardInfo.Name,
                        setName = c.CardInfo.CardSet?.Name,
                        imageUrl = c.CardInfo.ImageSmall ?? c.CardInfo.ImageUrl,
                        finish = c.Finish.ToString(),
                        quantity = c.Quantity,
                        unitValue = Math.Round(newUnit.Value * factor, 2),
                        unitChange = change,
                        totalChange = change * c.Quantity,
                        percent = Math.Round((newUnit.Value - oldUnit.Value) / oldUnit.Value * 100, 1)
                    };
                })
                .Where(m => m != null)
                .Select(m => m!)
                .ToList();

            var completedTrades = await _context.TradeOffers
                .CountAsync(t => (t.SenderId == userId || t.ReceiverId == userId) && t.Status == TradeOfferStatus.Completed);

            return Ok(new
            {
                totals = new
                {
                    totalCards = cards.Sum(c => c.Quantity),
                    uniqueCards = cards.Count,
                    marketValue = cards.Sum(LineValue),
                    availableForTrade = cards.Where(c => c.IsAvailableForTrade).Sum(c => c.Quantity),
                    tradeableValue = cards.Where(c => c.IsAvailableForTrade).Sum(LineValue),
                    unpricedCards = cards.Where(c => CardPricing.MarketValueEur(c) == null).Sum(c => c.Quantity)
                },
                change7d = Change(7),
                change30d = Change(30),
                series = series.Where(p => p.Date >= today.AddDays(-days)).Select(p => new { date = p.Date.ToString("yyyy-MM-dd"), value = p.Value }),
                topCards = cards
                    .Select(c => new { card = c, unit = CardPricing.MarketValueEur(c) })
                    .Where(x => x.unit.HasValue)
                    .OrderByDescending(x => x.unit)
                    .Take(10)
                    .Select(x => new
                    {
                        cardId = x.card.Id,
                        cardInfoId = x.card.CardInfoId,
                        name = x.card.CardInfo.Name,
                        setName = x.card.CardInfo.CardSet?.Name,
                        imageUrl = x.card.CardInfo.ImageSmall ?? x.card.CardInfo.ImageUrl,
                        finish = x.card.Finish.ToString(),
                        condition = x.card.Condition.ToString(),
                        quantity = x.card.Quantity,
                        unitValue = x.unit!.Value,
                        totalValue = x.unit!.Value * x.card.Quantity
                    }),
                gainers = movers.Where(m => m.totalChange > 0).OrderByDescending(m => m.totalChange).Take(5),
                losers = movers.Where(m => m.totalChange < 0).OrderBy(m => m.totalChange).Take(5),
                byGame = cards
                    .GroupBy(c => c.CardInfo.CardSet?.Game?.Name ?? "Altro")
                    .Select(g => new { game = g.Key, count = g.Sum(c => c.Quantity), value = g.Sum(LineValue) })
                    .OrderByDescending(g => g.value),
                byCondition = cards
                    .GroupBy(c => c.Condition)
                    .OrderBy(g => g.Key)
                    .Select(g => new { condition = g.Key.ToString(), count = g.Sum(c => c.Quantity), value = g.Sum(LineValue) }),
                trading = new { completedTrades }
            });
        }

        private Task<List<Card>> LoadCardsAsync(int userId) =>
            _context.Cards
                .AsNoTracking()
                .Include(c => c.CardInfo).ThenInclude(ci => ci.CardSet).ThenInclude(cs => cs.Game)
                .Where(c => c.UserId == userId)
                .ToListAsync();

        private static decimal LineValue(Card c) => (CardPricing.MarketValueEur(c) ?? 0) * c.Quantity;

        /// <summary>
        /// Statistiche globali della piattaforma (visibili a tutti)
        /// </summary>
        [HttpGet("platform")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPlatformStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalCards = await _context.Cards.CountAsync();
            var totalTrades = await _context.TradeOffers.CountAsync(t => t.Status == TradeOfferStatus.Completed);
            var totalGames = await _context.Games.CountAsync();
            var cardsAvailable = await _context.Cards.CountAsync(c => c.IsAvailableForTrade);

            return Ok(new
            {
                totalUsers,
                totalCards,
                totalTrades,
                totalGames,
                cardsAvailable
            });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
