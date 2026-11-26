using System.Text.RegularExpressions;

namespace CardExchange.Core.Rules
{
    /// <summary>
    /// Controlli sulla richiesta di inserimento di un negozio: tutti i campi sono obbligatori tranne Instagram.
    /// Le chiavi dei campi sono le stesse del modulo nel frontend, che le evidenzia.
    /// </summary>
    public static class StoreSubmissionRules
    {
        public record Fields(
            string? Name, string? Description, string? Address, string? City, string? Province, string? PostalCode,
            string? Country, decimal Latitude, decimal Longitude, string? Phone, string? Website, string? OpeningHours,
            IReadOnlyCollection<int> GameIds);

        public record Result(IReadOnlyList<string> Missing, IReadOnlyList<string> Invalid)
        {
            public bool IsValid => Missing.Count == 0 && Invalid.Count == 0;
        }

        public static Result Check(Fields f)
        {
            var missing = new List<string>();
            var invalid = new List<string>();
            void Required(string key, string? value) { if (string.IsNullOrWhiteSpace(value)) missing.Add(key); }

            Required("name", f.Name);
            Required("description", f.Description);
            Required("address", f.Address);
            Required("city", f.City);
            Required("province", f.Province);
            Required("postalCode", f.PostalCode);
            if (f.Latitude == 0 && f.Longitude == 0) missing.Add("location");
            Required("openingHours", f.OpeningHours);
            Required("phone", f.Phone);
            Required("website", f.Website);
            if (f.GameIds.Count == 0) missing.Add("games");

            if (!missing.Contains("name") && f.Name!.Trim().Length < 2) invalid.Add("name");
            if (!missing.Contains("description") && f.Description!.Trim().Length < 20) invalid.Add("description");
            if (!missing.Contains("postalCode") && (f.Country ?? "IT").Equals("IT", StringComparison.OrdinalIgnoreCase)
                && !Regex.IsMatch(f.PostalCode!.Trim(), @"^\d{5}$")) invalid.Add("postalCode");
            if (!missing.Contains("phone") && !IsPhone(f.Phone!)) invalid.Add("phone");
            if (!missing.Contains("website") && NormalizeWebsite(f.Website!) == null) invalid.Add("website");

            return new Result(missing, invalid);
        }

        /// <summary>Telefono: solo cifre, spazi, punti, trattini, parentesi e un + iniziale; da 6 a 15 cifre.</summary>
        public static bool IsPhone(string phone)
        {
            var p = phone.Trim();
            if (!Regex.IsMatch(p, @"^\+?[\d\s.\-()/]+$")) return false;
            var digits = p.Count(char.IsDigit);
            return digits is >= 6 and <= 15;
        }

        /// <summary>Sito con o senza http(s)://; null se non è un indirizzo web valido.</summary>
        public static string? NormalizeWebsite(string website)
        {
            var w = website.Trim();
            if (!w.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !w.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                w = "https://" + w;
            return Uri.TryCreate(w, UriKind.Absolute, out var uri) && uri.Host.Contains('.') && !uri.Host.EndsWith('.')
                ? uri.ToString().TrimEnd('/')
                : null;
        }
    }
}
