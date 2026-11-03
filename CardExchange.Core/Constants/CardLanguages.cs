namespace CardExchange.Core.Constants
{
    /// <summary>
    /// Lingue di stampa supportate. I codici seguono la convenzione Scryfall
    /// (usata anche dagli export di ManaBox e Moxfield), così l'import CSV non richiede conversioni.
    /// </summary>
    public static class CardLanguages
    {
        public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
        {
            ["en"] = "Inglese",
            ["it"] = "Italiano",
            ["fr"] = "Francese",
            ["de"] = "Tedesco",
            ["es"] = "Spagnolo",
            ["pt"] = "Portoghese",
            ["ja"] = "Giapponese",
            ["ko"] = "Coreano",
            ["zhs"] = "Cinese semplificato",
            ["zht"] = "Cinese tradizionale",
            ["ru"] = "Russo",
        };

        public static bool IsValid(string? code) => code != null && All.ContainsKey(code);

        /// <summary>Normalizza un codice (spazi, maiuscole). Restituisce null se vuoto o non supportato.</summary>
        public static string? Normalize(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var normalized = code.Trim().ToLowerInvariant();
            return All.ContainsKey(normalized) ? normalized : null;
        }
    }
}
