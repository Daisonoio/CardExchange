using System.Security.Cryptography;

namespace CardExchange.API.Services
{
    public static class ReferralCodes
    {
        // Senza caratteri ambigui (0/O, 1/I/L) per poterlo anche dettare a voce
        private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        public const int Length = 8;

        public static string Generate() =>
            string.Create(Length, 0, (span, _) =>
            {
                for (int i = 0; i < span.Length; i++) span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            });

        /// <summary>Normalizza il codice digitato (spazi, minuscole). Null se non ha un formato valido.</summary>
        public static string? Normalize(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var c = code.Trim().ToUpperInvariant();
            return c.Length == Length && c.All(ch => Alphabet.Contains(ch)) ? c : null;
        }
    }
}
