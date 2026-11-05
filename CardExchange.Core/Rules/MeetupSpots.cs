namespace CardExchange.Core.Rules
{
    /// <summary>
    /// Negozi da proporre come punto di scambio: i partner (piano Negozio attivo) per primi, poi per distanza
    /// dal punto medio tra i due utenti, così nessuno dei due deve fare tutta la strada.
    /// </summary>
    public static class MeetupSpots
    {
        public const double MaxDistanceKm = 30;
        public const int MaxSuggestions = 5;

        public record Candidate(int StoreId, double Latitude, double Longitude, bool IsPartner);
        public record Ranked(int StoreId, bool IsPartner, double FromMidpointKm);

        public static (double Lat, double Lon) Midpoint(double lat1, double lon1, double lat2, double lon2) =>
            ((lat1 + lat2) / 2, (lon1 + lon2) / 2);

        public static IReadOnlyList<Ranked> Rank(IEnumerable<Candidate> stores, double midLat, double midLon) =>
            stores
                .Select(s => new Ranked(s.StoreId, s.IsPartner, Geo.DistanceKm(midLat, midLon, s.Latitude, s.Longitude)))
                .Where(r => r.FromMidpointKm <= MaxDistanceKm)
                .OrderByDescending(r => r.IsPartner)
                .ThenBy(r => r.FromMidpointKm)
                .Take(MaxSuggestions)
                .ToList();
    }
}
