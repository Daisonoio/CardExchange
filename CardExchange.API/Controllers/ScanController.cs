using System.Globalization;
using System.Text;
using CardExchange.API.Services.Scan;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Scanner: riconosce la carta dall'impronta visiva calcolata sul telefono (la foto non arriva al server),
    /// aiutandosi con il nome e il numero letti dal testo quando ci sono.
    /// </summary>
    [ApiController]
    [Authorize]
    public class ScanController : ControllerBase
    {
        public const int MaxVectors = 12;
        private const double NameBoost = 0.06, NumberBoost = 0.08;
        private readonly ApplicationDbContext _db;
        private readonly CardFingerprintIndex _index;

        public ScanController(ApplicationDbContext db, CardFingerprintIndex index)
        {
            _db = db;
            _index = index;
        }

        public record MatchRequest(int? GameId, List<string> Vectors, List<string>? Names, string? Number);

        /// <summary>Quante carte del gioco lo scanner sa riconoscere dall'immagine.</summary>
        [HttpGet("api/scan/coverage")]
        public async Task<IActionResult> Coverage([FromQuery] int? gameId, CancellationToken ct) =>
            Ok(new { cards = await _index.CountAsync(gameId, ct), version = CardFingerprint.Version });

        [HttpPost("api/scan/match")]
        public async Task<IActionResult> Match([FromBody] MatchRequest request, CancellationToken ct)
        {
            var vectors = new List<sbyte[]>();
            foreach (var v in (request.Vectors ?? new()).Take(MaxVectors))
            {
                try
                {
                    var bytes = Convert.FromBase64String(v);
                    if (bytes.Length == CardFingerprint.Length) vectors.Add(CardFingerprint.FromBytes(bytes));
                }
                catch (FormatException) { }
            }
            if (vectors.Count == 0) return BadRequest(new { message = "Impronta della carta non valida" });

            var visual = await _index.MatchAsync(request.GameId, vectors, 30, ct);
            if (visual.Count == 0) return Ok(new { candidates = Array.Empty<object>(), indexed = false });

            var ids = visual.Select(v => v.CardInfoId).ToList();
            var infos = await _db.CardInfos.AsNoTracking().Include(ci => ci.CardSet)
                .Where(ci => ids.Contains(ci.Id))
                .ToDictionaryAsync(ci => ci.Id, ct);

            var names = (request.Names ?? new()).Select(Norm).Where(n => n.Length >= 3).Take(6).ToList();
            var number = NumberOf(request.Number);
            var ranked = visual.Where(v => infos.ContainsKey(v.CardInfoId)).Select(v =>
            {
                var ci = infos[v.CardInfoId];
                var name = Norm(ci.Name);
                var nameHit = names.Any(n => name == n || name.StartsWith(n + " ") || n.StartsWith(name + " ") || (n.Length >= 5 && name.Contains(n)));
                var numberHit = number != null && NumberOf(ci.CardNumber) == number;
                return new { ci, v.Score, Total = v.Score + (nameHit ? NameBoost : 0) + (numberHit ? NumberBoost : 0), nameHit, numberHit };
            }).OrderByDescending(x => x.Total).Take(8).ToList();

            var top = ranked[0];
            var second = ranked.Count > 1 ? ranked[1].Total : 0;
            // Sicura: ben staccata dalla seconda (le ristampe con la stessa illustrazione non lo sono), oppure confermata dal testo.
            // Soglie misurate su foto simulate: le risposte giuste hanno quasi sempre più di 0,1 di distacco.
            var confident = (top.Score >= 0.5 && top.Total - second >= 0.1) || (top.Score >= 0.45 && (top.nameHit || top.numberHit) && top.Total - second >= 0.03);

            return Ok(new
            {
                indexed = true,
                confident,
                candidates = ranked.Select(x => new
                {
                    cardInfoId = x.ci.Id,
                    externalId = x.ci.PokemonTcgId ?? x.ci.ScryfallId ?? x.ci.OnePieceTcgId ?? x.ci.YuGiOhId?.ToString(CultureInfo.InvariantCulture) ?? x.ci.ExternalId,
                    name = x.ci.Name,
                    setName = x.ci.CardSet?.Name,
                    number = x.ci.CardNumber,
                    rarity = x.ci.Rarity,
                    imageSmall = CardImageDecoder.ImageOf(x.ci) is { } img ? CardImageDecoder.SmallImageUrl(img) : null,
                    imageLarge = x.ci.ImageLarge ?? x.ci.ImageUrl,
                    score = Math.Round(x.Score, 3),
                    textMatch = x.nameHit || x.numberHit
                })
            });
        }

        /// <summary>Stato dell'indice delle immagini, per gli amministratori.</summary>
        [HttpGet("api/admin/scan/index")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> IndexStatus([FromServices] CardFingerprintIndexer indexer, CancellationToken ct) =>
            Ok(await indexer.StatusAsync(ct));

        /// <summary>Calcola subito le impronte mancanti (di solito avviene ogni notte).</summary>
        [HttpPost("api/admin/scan/index/run")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> RunIndex([FromServices] CardFingerprintIndexer indexer, [FromQuery] int max = 500, CancellationToken ct = default)
        {
            var done = await indexer.RunAsync(Math.Clamp(max, 1, 5000), DateTime.UtcNow, ct);
            return Ok(new { done, status = await indexer.StatusAsync(ct) });
        }

        private static string Norm(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
            }
            return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>"025/165" → "25", "OP01-001" → "op01-001": il numero confrontabile.</summary>
        private static string? NumberOf(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var first = s.Trim().Split('/')[0].Trim().ToLowerInvariant();
            return first.All(char.IsDigit) ? first.TrimStart('0') is { Length: > 0 } t ? t : "0" : first;
        }
    }
}
