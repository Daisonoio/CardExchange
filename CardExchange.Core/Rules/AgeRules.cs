namespace CardExchange.Core.Rules
{
    /// <summary>
    /// Età minima per usare il servizio. In Italia il consenso al trattamento dei dati nei servizi online
    /// si può dare da soli dai 14 anni (GDPR art. 8 e D.Lgs. 101/2018); sotto serve il consenso verificabile
    /// dei genitori, che l'app non raccoglie, quindi la registrazione non è consentita.
    /// </summary>
    public static class AgeRules
    {
        public const int DefaultMinimumAge = 14;
        public const int AdultAge = 18;
        public const int MaxPlausibleAge = 120;

        /// <summary>Anni compiuti alla data indicata. Chi è nato il 29 febbraio, negli anni non bisestili compie gli anni il 1° marzo (scelta prudente per un controllo di età minima).</summary>
        public static int AgeOn(DateOnly birthDate, DateOnly today)
        {
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age)) age--;
            return age;
        }

        /// <summary>Data plausibile: non nel futuro e non oltre i 120 anni.</summary>
        public static bool IsPlausible(DateOnly birthDate, DateOnly today) =>
            birthDate <= today && AgeOn(birthDate, today) <= MaxPlausibleAge;

        public static bool MeetsMinimum(DateOnly birthDate, DateOnly today, int minimumAge = DefaultMinimumAge) =>
            AgeOn(birthDate, today) >= minimumAge;

        public static bool IsMinor(DateOnly birthDate, DateOnly today) => AgeOn(birthDate, today) < AdultAge;
    }
}
