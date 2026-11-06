using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Catalog
{
    /// <summary>
    /// Da dove prendere le immagini One Piece. Le immagini pubbliche (sito Bandai e le API che le copiano)
    /// hanno la filigrana "SAMPLE"; quelle pulite vanno usate solo con i diritti verificati (licenza o accordo).
    /// Finché "Catalog:OnePiece:ImageUrlTemplate" è vuoto resta la sorgente attuale.
    /// Segnaposto del modello: {code} (OP01-001_p1, con la variante), {number} (OP01-001), {set} (OP01).
    /// </summary>
    public static class OnePieceImageSource
    {
        public const string ConfigKey = "Catalog:OnePiece:ImageUrlTemplate";

        public static string? Template(IConfiguration? configuration)
        {
            var t = configuration?[ConfigKey]?.Trim();
            return string.IsNullOrEmpty(t) || !t.Contains("{code}") && !t.Contains("{number}") ? null : t;
        }

        /// <summary>Indirizzo dell'immagine di una carta secondo il modello (null se il modello non c'è).</summary>
        public static string? UrlFor(string? code, string? template)
        {
            if (template == null || string.IsNullOrWhiteSpace(code)) return null;
            code = code.Trim().ToUpperInvariant().Replace("_P", "_p");
            var number = code.Split('_')[0];
            var dash = number.IndexOf('-');
            var set = dash > 0 ? number[..dash] : number;
            return template.Replace("{code}", Uri.EscapeDataString(code))
                .Replace("{number}", Uri.EscapeDataString(number))
                .Replace("{set}", Uri.EscapeDataString(set));
        }

        /// <summary>
        /// Porta le carte One Piece già nel catalogo sulla sorgente configurata (a blocchi, solo quelle da cambiare).
        /// Restituisce quante carte sono state aggiornate.
        /// </summary>
        public static async Task<int> BackfillAsync(ApplicationDbContext db, string? template, int batchSize = 500, CancellationToken ct = default)
        {
            if (template == null) return 0;
            var updated = 0;
            var lastId = 0;
            while (true)
            {
                var batch = await db.CardInfos.Where(ci => ci.OnePieceTcgId != null && ci.Id > lastId)
                    .OrderBy(ci => ci.Id).Take(batchSize).ToListAsync(ct);
                if (batch.Count == 0) break;
                lastId = batch[^1].Id;
                foreach (var ci in batch)
                {
                    var url = UrlFor(ci.OnePieceTcgId, template);
                    if (url == null || ci.ImageUrl == url && ci.ImageSmall == url && ci.ImageLarge == url) continue;
                    ci.ImageUrl = ci.ImageSmall = ci.ImageLarge = url;
                    ci.OnePieceImageSmall = ci.OnePieceImageLarge = url;
                    ci.ImageNormal = url;
                    updated++;
                }
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
            return updated;
        }
    }
}
