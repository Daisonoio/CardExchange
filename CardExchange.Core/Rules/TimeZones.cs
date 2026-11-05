using System.Collections.Concurrent;
using System.Globalization;

namespace CardExchange.Core.Rules
{
    /// <summary>
    /// Fusi orari: ogni utente ha il suo (rilevato dal browser), predefinito Europe/Rome.
    /// Gli orari nelle notifiche si scrivono nel fuso di chi le riceve.
    /// </summary>
    public static class TimeZones
    {
        public const string Default = "Europe/Rome";
        private static readonly ConcurrentDictionary<string, TimeZoneInfo?> Cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly CultureInfo Italian = new("it-IT");

        public static TimeZoneInfo Italy => Resolve(Default);

        /// <summary>Il fuso indicato, oppure quello italiano se mancante o sconosciuto.</summary>
        public static TimeZoneInfo Resolve(string? id) =>
            (string.IsNullOrWhiteSpace(id) ? null : Find(id)) ?? Find(Default) ?? Find("W. Europe Standard Time") ?? TimeZoneInfo.Utc;

        public static bool IsValid(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 64 && Find(id) != null;

        public static DateTime ToLocal(DateTime utc, string? id) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Resolve(id));

        /// <summary>Data leggibile, es. "sabato 8 novembre alle 15:30", nel fuso indicato.</summary>
        public static string FormatWhen(DateTime utc, string? id) =>
            ToLocal(utc, id).ToString("dddd d MMMM 'alle' HH:mm", Italian);

        /// <summary>Solo l'ora, es. "15:30", nel fuso indicato.</summary>
        public static string FormatTime(DateTime utc, string? id) => ToLocal(utc, id).ToString("HH:mm", CultureInfo.InvariantCulture);

        private static TimeZoneInfo? Find(string id) => Cache.GetOrAdd(id, key =>
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(key); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
        });
    }
}
