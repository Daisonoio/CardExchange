using CardExchange.API.Authorization;
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
    [Authorize]
    public class StatisticsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StatisticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Statistiche base della collezione (FREE)
        /// </summary>
        [HttpGet("collection/basic")]
        public async Task<IActionResult> GetBasicStats()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var totalCards = await _context.Cards.CountAsync(c => c.UserId == userId);
            var availableForTrade = await _context.Cards.CountAsync(c => c.UserId == userId && c.IsAvailableForTrade);
            var wishlistItems = await _context.WishlistItems.CountAsync(w => w.UserId == userId);
            var pendingTrades = await _context.TradeOffers.CountAsync(t =>
                (t.SenderId == userId || t.ReceiverId == userId) && t.Status == TradeOfferStatus.Pending);

            return Ok(new
            {
                totalCards,
                availableForTrade,
                wishlistItems,
                pendingTrades
            });
        }

        /// <summary>
        /// Statistiche avanzate della collezione (PREMIUM)
        /// </summary>
        [HttpGet("collection/advanced")]
        [RequirePremium]
        public async Task<IActionResult> GetAdvancedStats()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var cards = await _context.Cards
                .AsNoTracking()
                .Include(c => c.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            var totalValue = cards.Where(c => c.EstimatedValue.HasValue).Sum(c => c.EstimatedValue!.Value);

            var byGame = cards
                .GroupBy(c => c.CardInfo.CardSet.Game.Name)
                .Select(g => new
                {
                    game = g.Key,
                    count = g.Count(),
                    value = g.Where(c => c.EstimatedValue.HasValue).Sum(c => c.EstimatedValue!.Value)
                })
                .OrderByDescending(g => g.count)
                .ToList();

            var byCondition = cards
                .GroupBy(c => c.Condition.ToString())
                .Select(g => new { condition = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count)
                .ToList();

            var byRarity = cards
                .Where(c => !string.IsNullOrEmpty(c.CardInfo.Rarity))
                .GroupBy(c => c.CardInfo.Rarity!)
                .Select(g => new { rarity = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count)
                .ToList();

            var completedTrades = await _context.TradeOffers
                .CountAsync(t => (t.SenderId == userId || t.ReceiverId == userId) && t.Status == TradeOfferStatus.Completed);

            var user = await _context.Users.FindAsync(userId);

            return Ok(new
            {
                portfolio = new
                {
                    totalCards = cards.Count,
                    totalValue,
                    averageValue = cards.Count > 0 ? totalValue / cards.Count : 0,
                    highestValue = cards.Where(c => c.EstimatedValue.HasValue).MaxBy(c => c.EstimatedValue)?.EstimatedValue ?? 0
                },
                byGame,
                byCondition,
                byRarity,
                trading = new
                {
                    completedTrades,
                    reputationScore = user?.ReputationScore ?? 0,
                    totalReviews = user?.TotalReviewsReceived ?? 0
                }
            });
        }

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
