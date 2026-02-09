using CardExchange.API.Authorization;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace CardExchange.API.Controllers
{
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

        /// <summary>
        /// Esporta la collezione in formato CSV (PREMIUM)
        /// </summary>
        [HttpGet("collection/csv")]
        [RequirePremium]
        public async Task<IActionResult> ExportCollectionCsv()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var cards = await _context.Cards
                .AsNoTracking()
                .Include(c => c.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.CardInfo.CardSet.Game.Name)
                    .ThenBy(c => c.CardInfo.CardSet.Name)
                    .ThenBy(c => c.CardInfo.Name)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Gioco,Set,Codice Set,Nome Carta,Numero,Rarità,Tipo,Condizione,Quantità,Disponibile Scambio,Valore Stimato,Note");

            foreach (var card in cards)
            {
                var fields = new[]
                {
                    EscapeCsv(card.CardInfo.CardSet.Game.Name),
                    EscapeCsv(card.CardInfo.CardSet.Name),
                    EscapeCsv(card.CardInfo.CardSet.Code),
                    EscapeCsv(card.CardInfo.Name),
                    EscapeCsv(card.CardInfo.CardNumber ?? ""),
                    EscapeCsv(card.CardInfo.Rarity ?? ""),
                    EscapeCsv(card.CardInfo.Type ?? ""),
                    card.Condition.ToString(),
                    card.Quantity.ToString(),
                    card.IsAvailableForTrade ? "Sì" : "No",
                    card.EstimatedValue?.ToString("F2") ?? "",
                    EscapeCsv(card.Notes ?? "")
                };
                sb.AppendLine(string.Join(",", fields));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"collezione_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        /// <summary>
        /// Esporta la collezione in formato JSON (PREMIUM)
        /// </summary>
        [HttpGet("collection/json")]
        [RequirePremium]
        public async Task<IActionResult> ExportCollectionJson()
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

            var export = new
            {
                exportDate = DateTime.UtcNow,
                totalCards = cards.Count,
                totalValue = cards.Where(c => c.EstimatedValue.HasValue).Sum(c => c.EstimatedValue!.Value),
                cards = cards.Select(c => new
                {
                    game = c.CardInfo.CardSet.Game.Name,
                    cardSet = c.CardInfo.CardSet.Name,
                    cardSetCode = c.CardInfo.CardSet.Code,
                    name = c.CardInfo.Name,
                    cardNumber = c.CardInfo.CardNumber,
                    rarity = c.CardInfo.Rarity,
                    type = c.CardInfo.Type,
                    condition = c.Condition.ToString(),
                    quantity = c.Quantity,
                    isAvailableForTrade = c.IsAvailableForTrade,
                    estimatedValue = c.EstimatedValue,
                    notes = c.Notes
                })
            };

            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            return File(Encoding.UTF8.GetBytes(json), "application/json", $"collezione_{DateTime.UtcNow:yyyyMMdd}.json");
        }

        /// <summary>
        /// Esporta la wishlist in CSV (PREMIUM)
        /// </summary>
        [HttpGet("wishlist/csv")]
        [RequirePremium]
        public async Task<IActionResult> ExportWishlistCsv()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var items = await _context.WishlistItems
                .AsNoTracking()
                .Include(w => w.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Where(w => w.UserId == userId)
                .OrderBy(w => w.Priority)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Gioco,Set,Nome Carta,Rarità,Condizione Preferita,Prezzo Max,Priorità,Note");

            foreach (var item in items)
            {
                var fields = new[]
                {
                    EscapeCsv(item.CardInfo.CardSet.Game.Name),
                    EscapeCsv(item.CardInfo.CardSet.Name),
                    EscapeCsv(item.CardInfo.Name),
                    EscapeCsv(item.CardInfo.Rarity ?? ""),
                    item.PreferredCondition?.ToString() ?? "Qualsiasi",
                    item.MaxPrice?.ToString("F2") ?? "",
                    item.Priority.ToString(),
                    EscapeCsv(item.Notes ?? "")
                };
                sb.AppendLine(string.Join(",", fields));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"wishlist_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
