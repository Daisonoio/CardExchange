using System.Text;

namespace CardExchange.API.Services.Import
{
    /// <summary>
    /// Lettore CSV minimale (RFC 4180): virgolette, virgolette raddoppiate, a capo dentro i campi,
    /// BOM, riga "sep=;" di Excel e rilevamento automatico del separatore (, ; tab).
    /// </summary>
    public sealed class CsvTable
    {
        public IReadOnlyList<string> Headers { get; }
        /// <summary>Righe di dati con il numero di riga del file (1 = intestazione).</summary>
        public IReadOnlyList<(int LineNumber, IReadOnlyList<string> Values)> Rows { get; }
        public char Delimiter { get; }

        private CsvTable(IReadOnlyList<string> headers, IReadOnlyList<(int, IReadOnlyList<string>)> rows, char delimiter)
        {
            Headers = headers;
            Rows = rows;
            Delimiter = delimiter;
        }

        public static CsvTable Parse(string text)
        {
            text = text.TrimStart('﻿');
            int headerLine = 1;

            char? explicitSep = null;
            var firstLineEnd = text.IndexOfAny(new[] { '\r', '\n' });
            var firstLine = firstLineEnd >= 0 ? text[..firstLineEnd] : text;
            if (firstLine.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) && firstLine.Length >= 5)
            {
                explicitSep = firstLine[4];
                text = firstLineEnd >= 0 ? text[(firstLineEnd + 1)..].TrimStart('\n') : string.Empty;
                headerLine = 2;
                firstLineEnd = text.IndexOfAny(new[] { '\r', '\n' });
                firstLine = firstLineEnd >= 0 ? text[..firstLineEnd] : text;
            }

            var delimiter = explicitSep ?? DetectDelimiter(firstLine);
            var records = ReadRecords(text, delimiter);
            if (records.Count == 0)
                return new CsvTable(Array.Empty<string>(), Array.Empty<(int, IReadOnlyList<string>)>(), delimiter);

            var headers = records[0].Values.Select(h => h.Trim()).ToList();
            var rows = records.Skip(1)
                .Where(r => r.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                .Select(r => (r.Line + headerLine - 1, (IReadOnlyList<string>)r.Values))
                .ToList();
            return new CsvTable(headers, rows, delimiter);
        }

        private static char DetectDelimiter(string headerLine)
        {
            var candidates = new[] { ',', ';', '\t' };
            return candidates.OrderByDescending(c => CountOutsideQuotes(headerLine, c)).First();
        }

        private static int CountOutsideQuotes(string line, char c)
        {
            int count = 0;
            bool inQuotes = false;
            foreach (var ch in line)
            {
                if (ch == '"') inQuotes = !inQuotes;
                else if (ch == c && !inQuotes) count++;
            }
            return count;
        }

        private static List<(int Line, List<string> Values)> ReadRecords(string text, char delimiter)
        {
            var records = new List<(int, List<string>)>();
            var current = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            int line = 1, recordStartLine = 1;

            for (int i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else
                    {
                        if (ch == '\n') line++;
                        field.Append(ch);
                    }
                    continue;
                }

                if (ch == '"' && field.Length == 0) { inQuotes = true; }
                else if (ch == delimiter) { current.Add(field.ToString()); field.Clear(); }
                else if (ch == '\r') { /* gestito con \n */ }
                else if (ch == '\n')
                {
                    current.Add(field.ToString()); field.Clear();
                    records.Add((recordStartLine, current));
                    current = new List<string>();
                    line++;
                    recordStartLine = line;
                }
                else field.Append(ch);
            }

            if (field.Length > 0 || current.Count > 0)
            {
                current.Add(field.ToString());
                records.Add((recordStartLine, current));
            }
            return records;
        }
    }
}
