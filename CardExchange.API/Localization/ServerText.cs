using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CardExchange.API.Localization
{
    /// <summary>
    /// Traduzioni dei testi del server (messaggi delle API, notifiche, email). Il testo di partenza è l'italiano
    /// scritto nel codice; il dizionario (server-text.en.json) ha frasi esatte e modelli con segnaposto:
    /// {0}, {1}… copiano il valore (tradotto se è a sua volta una frase nota), {n} e {m} solo numeri, {when} riconosce una data italiana ("sabato 8 novembre alle 15:30")
    /// e la riscrive nella lingua di destinazione. Un testo senza traduzione resta in italiano.
    /// </summary>
    public static class ServerText
    {
        public const string Default = "it";
        public static readonly string[] Supported = { "it", "en" };

        private record Pattern(Regex Regex, string Template, string[] Groups);
        private record Catalog(Dictionary<string, string> Exact, List<Pattern> Patterns);

        private static readonly Lazy<Dictionary<string, Catalog>> Catalogs = new(Load);

        private const string WhenRegex = @"(?:lunedì|martedì|mercoledì|giovedì|venerdì|sabato|domenica) \d{1,2} (?:gennaio|febbraio|marzo|aprile|maggio|giugno|luglio|agosto|settembre|ottobre|novembre|dicembre)(?: \d{4})? alle \d{1,2}:\d{2}";
        private const string MonthRegex = @"(?:gennaio|febbraio|marzo|aprile|maggio|giugno|luglio|agosto|settembre|ottobre|novembre|dicembre) \d{4}";

        /// <summary>"en-US,en;q=0.9" → "en"; lingue non supportate → italiano.</summary>
        public static string Normalize(string? language)
        {
            var primary = (language ?? "").Split(',')[0].Split(';')[0].Trim().Split('-')[0].ToLowerInvariant();
            return Supported.Contains(primary) ? primary : Default;
        }

        public static string T(string? text, string? language)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            var lang = Normalize(language);
            if (lang == Default || !Catalogs.Value.TryGetValue(lang, out var cat)) return text;
            if (cat.Exact.TryGetValue(text, out var exact)) return exact;
            foreach (var p in cat.Patterns)
            {
                var m = p.Regex.Match(text);
                if (!m.Success) continue;
                var result = p.Template;
                for (var i = 0; i < p.Groups.Length; i++)
                {
                    var value = m.Groups["g" + i].Value;
                    result = result.Replace("{" + p.Groups[i] + "}", p.Groups[i] switch
                    {
                        "when" => TranslateWhen(value, lang),
                        "month" => TranslateMonth(value, lang),
                        "n" or "m" => value,
                        _ => T(value, lang) // un valore può essere a sua volta una frase nota
                    });
                }
                return result;
            }
            return text;
        }

        private static readonly CultureInfo Italian = new("it-IT");

        private static string TranslateWhen(string italian, string lang)
        {
            var formats = new[] { "dddd d MMMM 'alle' HH:mm", "dddd d MMMM yyyy 'alle' HH:mm", "dddd d MMMM 'alle' H:mm" };
            if (!DateTime.TryParseExact(italian, formats, Italian, DateTimeStyles.None, out var d)) return italian;
            var hasYear = Regex.IsMatch(italian, @"\d{4}");
            var culture = new CultureInfo(lang == "en" ? "en-GB" : lang);
            // Senza anno il giorno della settimana va ricalcolato sull'anno più vicino a oggi
            if (!hasYear)
            {
                var weekday = italian.Split(' ')[0];
                for (var y = DateTime.UtcNow.Year - 1; y <= DateTime.UtcNow.Year + 1; y++)
                {
                    var candidate = new DateTime(y, d.Month, d.Day, d.Hour, d.Minute, 0);
                    if (candidate.ToString("dddd", Italian) == weekday) { d = candidate; break; }
                }
            }
            return d.ToString(hasYear ? "dddd d MMMM yyyy 'at' HH:mm" : "dddd d MMMM 'at' HH:mm", culture);
        }

        private static string TranslateMonth(string italian, string lang) =>
            DateTime.TryParseExact(italian, "MMMM yyyy", Italian, DateTimeStyles.None, out var d)
                ? d.ToString("MMMM yyyy", new CultureInfo(lang == "en" ? "en-GB" : lang))
                : italian;

        private static Dictionary<string, Catalog> Load()
        {
            var result = new Dictionary<string, Catalog>();
            foreach (var lang in Supported.Where(l => l != Default))
            {
                var exact = new Dictionary<string, string>(StringComparer.Ordinal);
                var patterns = new List<Pattern>();
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"CardExchange.API.Localization.server-text.{lang}.json");
                if (stream == null) continue;
                using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var source = prop.Name;
                    var target = prop.Value.GetString() ?? source;
                    if (!source.Contains('{')) { exact[source] = target; continue; }
                    var groups = new List<string>();
                    var regex = "^" + Regex.Replace(Regex.Escape(source), @"\\\{(\w+)\}", m =>
                    {
                        var name = m.Groups[1].Value;
                        var index = groups.Count;
                        groups.Add(name);
                        var body = name switch { "when" => WhenRegex, "month" => MonthRegex, "n" or "m" => @"\d+", _ => ".+?" };
                        return $"(?<g{index}>{body})";
                    }) + "$";
                    patterns.Add(new Pattern(new Regex(regex, RegexOptions.Compiled | RegexOptions.Singleline), target, groups.ToArray()));
                }
                // Prima i modelli più specifici (più testo fisso)
                patterns.Sort((a, b) => b.Regex.ToString().Length.CompareTo(a.Regex.ToString().Length));
                result[lang] = new Catalog(exact, patterns);
            }
            return result;
        }
    }
}
