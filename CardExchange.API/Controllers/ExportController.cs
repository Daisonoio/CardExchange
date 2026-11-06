using CardExchange.API.Authorization;
using CardExchange.API.Services.Import;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Esportazione di collezione e wishlist (Premium). Il CSV della collezione usa intestazioni che l'import di
    /// CardExchange e le principali app (ManaBox, Moxfield) riconoscono, così si può reimportare.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ExportController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("collection/csv")]
        [RequireCapability(Capability.ExportCollection)]
        public async Task<IActionResult> ExportCollectionCsv()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();
            var cards = await LoadCardsAsync(userId);

            var header = new[]
            {
                "Game", "Name", "Set code", "Set name", "Collector number", "Quantity", "Condition", "Language", "Finish",
                "Rarity", "For trade", "Market value EUR", "Asking price EUR", "Scryfall ID", "Notes"
            };
            var rows = cards.Select(c => new[]
            {
                c.CardInfo.CardSet?.Game?.Name,
                c.CardInfo.Name,
                c.CardInfo.CardSet?.Code,
                c.CardInfo.CardSet?.Name,
                c.CardInfo.CardNumber,
                c.Quantity.ToString(),
                c.Condition.ToString(),
                c.Language,
                c.Finish.ToString(),
                c.CardInfo.Rarity,
                c.IsAvailableForTrade ? "yes" : "no",
                CsvExport.Number(CardPricing.MarketValueEur(c)),
                CsvExport.Number(c.AskingPrice),
                c.CardInfo.ScryfallId,
                c.Notes
            });
            return File(CsvExport.ToBytes(header, rows), "text/csv; charset=utf-8", $"cardexchange_collezione_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        [HttpGet("collection/json")]
        [RequireCapability(Capability.ExportCollection)]
        public async Task<IActionResult> ExportCollectionJson()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();
            var cards = await LoadCardsAsync(userId);

            var export = new
            {
                exportDate = DateTime.UtcNow,
                totalCards = cards.Sum(c => c.Quantity),
                uniqueCards = cards.Count,
                marketValueEur = cards.Sum(c => (CardPricing.MarketValueEur(c) ?? 0) * c.Quantity),
                cards = cards.Select(c => new
                {
                    game = c.CardInfo.CardSet?.Game?.Name,
                    name = c.CardInfo.Name,
                    setCode = c.CardInfo.CardSet?.Code,
                    setName = c.CardInfo.CardSet?.Name,
                    collectorNumber = c.CardInfo.CardNumber,
                    rarity = c.CardInfo.Rarity,
                    quantity = c.Quantity,
                    condition = c.Condition.ToString(),
                    language = c.Language,
                    finish = c.Finish.ToString(),
                    forTrade = c.IsAvailableForTrade,
                    marketValueEur = CardPricing.MarketValueEur(c),
                    askingPriceEur = c.AskingPrice,
                    scryfallId = c.CardInfo.ScryfallId,
                    notes = c.Notes
                })
            };
            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return File(Encoding.UTF8.GetBytes(json), "application/json", $"cardexchange_collezione_{DateTime.UtcNow:yyyyMMdd}.json");
        }

        [HttpGet("wishlist/csv")]
        [RequireCapability(Capability.ExportCollection)]
        public async Task<IActionResult> ExportWishlistCsv()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var items = await _context.WishlistItems
                .AsNoTracking()
                .Include(w => w.CardInfo).ThenInclude(ci => ci.CardSet).ThenInclude(cs => cs.Game)
                .Where(w => w.UserId == userId)
                .OrderBy(w => w.Priority)
                .ToListAsync();

            var header = new[] { "Game", "Name", "Set code", "Set name", "Collector number", "Quantity", "Condition", "Language", "Finish", "Max price EUR", "Priority", "Notes" };
            var rows = items.Select(w => new[]
            {
                w.CardInfo.CardSet?.Game?.Name,
                w.CardInfo.Name,
                w.CardInfo.CardSet?.Code,
                w.CardInfo.CardSet?.Name,
                w.CardInfo.CardNumber,
                "1",
                w.PreferredCondition?.ToString(),
                w.PreferredLanguage,
                w.PreferredFinish?.ToString(),
                CsvExport.Number(w.MaxPrice),
                w.Priority.ToString(),
                w.Notes
            });
            return File(CsvExport.ToBytes(header, rows), "text/csv; charset=utf-8", $"cardexchange_wishlist_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        private Task<List<CardExchange.Core.Entities.Card>> LoadCardsAsync(int userId) =>
            _context.Cards
                .AsNoTracking()
                .Include(c => c.CardInfo).ThenInclude(ci => ci.CardSet).ThenInclude(cs => cs.Game)
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.CardInfo.CardSet.Game.Name).ThenBy(c => c.CardInfo.CardSet.Name).ThenBy(c => c.CardInfo.Name)
                .ToListAsync();

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
