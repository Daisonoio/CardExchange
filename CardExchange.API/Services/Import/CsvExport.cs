using System.Globalization;
using System.Text;

namespace CardExchange.API.Services.Import
{
    /// <summary>Scrittura CSV sicura (RFC 4180, UTF-8 con BOM per Excel).</summary>
    public static class CsvExport
    {
        /// <summary>
        /// Racchiude tra virgolette quando serve e neutralizza i valori che un foglio di calcolo eseguirebbe come
        /// formula (=, +, -, @, tab, a capo all'inizio): note e nomi sono scritti dagli utenti.
        /// </summary>
        public static string Field(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }

        public static string Number(decimal? value) =>
            value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty;

        public static byte[] ToBytes(IEnumerable<string> header, IEnumerable<IEnumerable<string?>> rows)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", header.Select(Field))).Append("\r\n");
            foreach (var row in rows)
                sb.Append(string.Join(",", row.Select(Field))).Append("\r\n");
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }
    }
}
