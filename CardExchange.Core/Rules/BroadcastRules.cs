namespace CardExchange.Core.Rules
{
    /// <summary>Regole degli avvisi "Sono qui" (durata, limiti anti-spam, testo della notifica).</summary>
    public static class BroadcastRules
    {
        /// <summary>Durata minima e massima di un avviso.</summary>
        public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(12);

        /// <summary>Avvisi al giorno per autore: abbastanza per spostarsi tra due posti, non per fare spam.</summary>
        public const int MaxPerDay = 3;

        public static TimeSpan ClampDuration(int minutes)
        {
            var d = TimeSpan.FromMinutes(minutes);
            if (d < MinDuration) return MinDuration;
            return d > MaxDuration ? MaxDuration : d;
        }

        public static bool IsValidCoordinate(decimal lat, decimal lon) =>
            lat is >= -90 and <= 90 && lon is >= -180 and <= 180 && !(lat == 0 && lon == 0);

        /// <summary>Distanza mostrata a chi riceve l'avviso: a km interi, minimo 1 (come in Esplora).</summary>
        public static int? DisplayDistanceKm(double? km) =>
            km.HasValue ? Math.Max(1, (int)Math.Round(km.Value)) : null;

        /// <summary>Titolo della notifica, es. "@mario è a 15 km da te".</summary>
        public static string Title(string authorName, int? distanceKm) =>
            distanceKm.HasValue
                ? $"{authorName} è a {distanceKm} km da te"
                : $"{authorName} ti dice dov'è";

        /// <summary>Testo della notifica: luogo, punto preciso e orario di fine (ora locale italiana).</summary>
        public static string Body(string placeName, string? spotDetail, DateTime endsAtUtc, string? message)
        {
            var until = TimeZoneInfo.ConvertTimeFromUtc(endsAtUtc, ItalyTimeZone()).ToString("HH:mm");
            var parts = new List<string> { placeName };
            if (!string.IsNullOrWhiteSpace(spotDetail)) parts.Add(spotDetail.Trim());
            parts.Add($"fino alle {until}");
            var body = string.Join(" · ", parts);
            if (!string.IsNullOrWhiteSpace(message))
            {
                var m = message.Trim();
                body += " — " + (m.Length > 100 ? m[..100] + "…" : m);
            }
            return body.Length > 500 ? body[..500] : body;
        }

        private static TimeZoneInfo ItalyTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); }
        }
    }
}
