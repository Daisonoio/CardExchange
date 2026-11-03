namespace CardExchange.Core.Rules
{
    public static class Geo
    {
        /// <summary>Distanza in km tra due coordinate (formula dell'emisenoverso).</summary>
        public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            static double ToRad(double d) => d * Math.PI / 180;
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        public static double DistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2) =>
            DistanceKm((double)lat1, (double)lon1, (double)lat2, (double)lon2);
    }
}
