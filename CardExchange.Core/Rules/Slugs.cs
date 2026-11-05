using System.Globalization;
using System.Text;

namespace CardExchange.Core.Rules
{
    /// <summary>Identificativi leggibili per link e QR: "Games Corner Milano!" -> "games-corner-milano".</summary>
    public static class Slugs
    {
        public const int MaxLength = 60;

        public static string From(string text)
        {
            var normalized = (text ?? "").Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            var dash = false;
            foreach (var c in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark) continue; // accenti
                if (char.IsAsciiLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    dash = false;
                }
                else if (!dash && sb.Length > 0)
                {
                    sb.Append('-');
                    dash = true;
                }
                if (sb.Length >= MaxLength) break;
            }
            var slug = sb.ToString().Trim('-');
            return slug.Length == 0 ? "negozio" : slug;
        }

        /// <summary>Primo identificativo libero: "nome", poi "nome-2", "nome-3"...</summary>
        public static string Unique(string baseSlug, Func<string, bool> taken)
        {
            if (!taken(baseSlug)) return baseSlug;
            for (var i = 2; ; i++)
            {
                var candidate = $"{baseSlug}-{i}";
                if (!taken(candidate)) return candidate;
            }
        }
    }
}
