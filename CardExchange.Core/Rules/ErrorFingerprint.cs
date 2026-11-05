using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CardExchange.Core.Rules
{
    /// <summary>
    /// Impronta di un errore per raggrupparlo con quelli uguali: numeri, id e GUID vengono ignorati,
    /// così "Carta 123 non trovata" e "Carta 456 non trovata" finiscono nello stesso gruppo.
    /// </summary>
    public static class ErrorFingerprint
    {
        private static readonly Regex Variable = new(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Normalize(string message) => Variable.Replace(message ?? "", "#").Trim();

        /// <summary>Prima riga utile dello stack ("at Classe.Metodo"), senza numeri di riga.</summary>
        public static string TopFrame(string? stack)
        {
            if (string.IsNullOrWhiteSpace(stack)) return "";
            var line = stack.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("at ")) ?? stack.Split('\n')[0];
            return Normalize(Regex.Replace(line, @"\s+in\s+.*$|:line \d+|:\d+:\d+\)?$", ""));
        }

        public static string Compute(string source, string category, string message, string? stack)
        {
            var input = $"{source}|{category}|{Normalize(message)}|{TopFrame(stack)}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..32].ToLowerInvariant();
        }
    }
}
