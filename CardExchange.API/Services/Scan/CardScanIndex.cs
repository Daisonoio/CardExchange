using System.Collections.Concurrent;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace CardExchange.API.Services.Scan
{
    /// <summary>Decodifica un'immagine e la porta a 252 × 352 RGBA, l'ingresso dell'impronta visiva.</summary>
    public static class CardImageDecoder
    {
        public static byte[]? ToFingerprintInput(byte[] encoded)
        {
            using var src = SKBitmap.Decode(encoded);
            if (src == null || src.Width < 20 || src.Height < 20) return null;
            var info = new SKImageInfo(CardFingerprint.InputWidth, CardFingerprint.InputHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var resized = src.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            if (resized == null) return null;
            var bytes = resized.Bytes;
            return bytes.Length == CardFingerprint.InputWidth * CardFingerprint.InputHeight * 4 ? bytes : null;
        }

        /// <summary>Versione piccola dell'immagine quando la fonte la offre (meno traffico per l'indice).</summary>
        public static string SmallImageUrl(string url)
        {
            if (url.Contains("images.pokemontcg.io") && url.EndsWith("_hires.png")) return url[..^"_hires.png".Length] + ".png";
            if (url.Contains("images.scrydex.com") && url.EndsWith("/large")) return url[..^"/large".Length] + "/small";
            return url;
        }

        public static string? ImageOf(CardInfo ci) => ci.ImageSmall ?? ci.ImageNormal ?? ci.ImageUrl ?? ci.ImageLarge;
    }

    /// <summary>
    /// Calcola le impronte delle carte del catalogo che non ce l'hanno ancora (a blocchi, ogni notte e su richiesta).
    /// Un'immagine che non si riesce a leggere si riprova dopo una settimana.
    /// </summary>
    public class CardFingerprintIndexer
    {
        private static readonly TimeSpan RetryFailedAfter = TimeSpan.FromDays(7);
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<CardFingerprintIndexer> _logger;

        public CardFingerprintIndexer(ApplicationDbContext db, IHttpClientFactory http, ILogger<CardFingerprintIndexer> logger)
        {
            _db = db;
            _http = http;
            _logger = logger;
        }

        public record Status(int Cards, int Indexed, int Failed);

        public async Task<Status> StatusAsync(CancellationToken ct = default)
        {
            var cards = await _db.CardInfos.CountAsync(ci => ci.ImageSmall != null || ci.ImageNormal != null || ci.ImageUrl != null || ci.ImageLarge != null, ct);
            var indexed = await _db.CardImageFingerprints.CountAsync(f => !f.Failed && f.Version == CardFingerprint.Version, ct);
            var failed = await _db.CardImageFingerprints.CountAsync(f => f.Failed, ct);
            return new(cards, indexed, failed);
        }

        /// <summary>Indicizza fino a <paramref name="max"/> carte; restituisce quante sono state elaborate.</summary>
        public async Task<int> RunAsync(int max, DateTime nowUtc, CancellationToken ct = default)
        {
            var retryBefore = nowUtc - RetryFailedAfter;
            var todo = await _db.CardInfos.AsNoTracking()
                .Where(ci => ci.ImageSmall != null || ci.ImageNormal != null || ci.ImageUrl != null || ci.ImageLarge != null)
                .Where(ci => !_db.CardImageFingerprints.Any(f => f.CardInfoId == ci.Id
                    && f.Version == CardFingerprint.Version && (!f.Failed || f.ComputedAt > retryBefore)))
                .OrderByDescending(ci => ci.Id)
                .Select(ci => new { ci.Id, ci.ImageSmall, ci.ImageNormal, ci.ImageUrl, ci.ImageLarge })
                .Take(max).ToListAsync(ct);
            if (todo.Count == 0) return 0;

            var client = _http.CreateClient("card-images");
            var results = new ConcurrentDictionary<int, sbyte[]?>();
            await Parallel.ForEachAsync(todo, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (c, token) =>
            {
                var url = CardImageDecoder.SmallImageUrl(c.ImageSmall ?? c.ImageNormal ?? c.ImageUrl ?? c.ImageLarge!);
                try
                {
                    var bytes = await client.GetByteArrayAsync(url, token);
                    var input = CardImageDecoder.ToFingerprintInput(bytes);
                    results[c.Id] = input == null ? null : CardFingerprint.FromRgba(input);
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    _logger.LogDebug(ex, "Immagine non disponibile per l'impronta: {Url}", url);
                    results[c.Id] = null;
                }
            });

            var ids = results.Keys.ToList();
            var existing = await _db.CardImageFingerprints.Where(f => ids.Contains(f.CardInfoId)).ToDictionaryAsync(f => f.CardInfoId, ct);
            foreach (var (id, vector) in results)
            {
                if (!existing.TryGetValue(id, out var f))
                {
                    f = new CardImageFingerprint { CardInfoId = id };
                    _db.CardImageFingerprints.Add(f);
                }
                f.Vector = vector == null ? Array.Empty<byte>() : CardFingerprint.ToBytes(vector);
                f.Failed = vector == null;
                f.Version = CardFingerprint.Version;
                f.ComputedAt = nowUtc;
            }
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Impronte visive: {Done} carte elaborate, {Failed} immagini non leggibili", results.Count, results.Values.Count(v => v == null));
            return results.Count;
        }
    }

    public record ScanCandidate(int CardInfoId, double Score);

    /// <summary>
    /// Indice in memoria delle impronte, per gioco. Si ricarica quando cambiano le impronte (al massimo ogni 5 minuti).
    /// </summary>
    public class CardFingerprintIndex
    {
        private static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(5);
        private readonly IServiceScopeFactory _scopes;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private Entry[] _entries = Array.Empty<Entry>();
        private (int Count, DateTime Latest) _stamp;
        private DateTime _checkedAt = DateTime.MinValue;

        private sealed record Entry(int CardInfoId, int GameId, sbyte[] Vector);

        public CardFingerprintIndex(IServiceScopeFactory scopes) => _scopes = scopes;

        public int Count => _entries.Length;

        private async Task EnsureLoadedAsync(CancellationToken ct)
        {
            if (DateTime.UtcNow - _checkedAt < CheckEvery && _entries.Length > 0) return;
            await _lock.WaitAsync(ct);
            try
            {
                if (DateTime.UtcNow - _checkedAt < CheckEvery && _entries.Length > 0) return;
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var valid = db.CardImageFingerprints.AsNoTracking().Where(f => !f.Failed && f.Version == CardFingerprint.Version);
                var count = await valid.CountAsync(ct);
                var latest = count == 0 ? DateTime.MinValue : await valid.MaxAsync(f => f.ComputedAt, ct);
                if ((count, latest) != _stamp || _entries.Length == 0)
                {
                    var rows = await valid.Select(f => new { f.CardInfoId, f.CardInfo.CardSet.GameId, f.Vector }).ToListAsync(ct);
                    _entries = rows.Where(r => r.Vector.Length == CardFingerprint.Length)
                        .Select(r => new Entry(r.CardInfoId, r.GameId, CardFingerprint.FromBytes(r.Vector))).ToArray();
                    _stamp = (count, latest);
                }
                _checkedAt = DateTime.UtcNow;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Le carte più simili a una delle impronte della foto (si tiene la migliore per carta).</summary>
        public async Task<IReadOnlyList<ScanCandidate>> MatchAsync(int? gameId, IReadOnlyList<sbyte[]> queries, int take, CancellationToken ct = default)
        {
            await EnsureLoadedAsync(ct);
            var entries = _entries;
            var scored = new List<ScanCandidate>(entries.Length);
            foreach (var e in entries)
            {
                if (gameId.HasValue && e.GameId != gameId.Value) continue;
                var best = double.MinValue;
                foreach (var q in queries)
                {
                    var s = CardFingerprint.Similarity(q, e.Vector);
                    if (s > best) best = s;
                }
                scored.Add(new ScanCandidate(e.CardInfoId, best));
            }
            return scored.OrderByDescending(s => s.Score).Take(take).ToList();
        }

        public async Task<int> CountAsync(int? gameId, CancellationToken ct = default)
        {
            await EnsureLoadedAsync(ct);
            return gameId.HasValue ? _entries.Count(e => e.GameId == gameId.Value) : _entries.Length;
        }
    }
}
