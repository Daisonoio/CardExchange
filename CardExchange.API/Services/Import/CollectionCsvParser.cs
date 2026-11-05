using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CardExchange.Core.Constants;
using CardExchange.Core.Entities;

namespace CardExchange.API.Services.Import
{
    public enum ImportField { Name, SetCode, SetName, Edition, CollectorNumber, ScryfallId, Quantity, Condition, Language, Finish, Game, Grade }

    public class ParsedCollectionRow
    {
        public int LineNumber { get; set; }
        public string? Name { get; set; }
        public string? SetCode { get; set; }
        public string? SetName { get; set; }
        public string? CollectorNumber { get; set; }
        public string? ScryfallId { get; set; }
        /// <summary>Gioco indicato nel file (es. export di CardExchange con più giochi).</summary>
        public string? Game { get; set; }
        public int Quantity { get; set; } = 1;
        public CardCondition Condition { get; set; } = CardCondition.NearMint;
        public string? Language { get; set; }
        public CardFinish Finish { get; set; } = CardFinish.Normal;
        /// <summary>Problemi non bloccanti (es. condizione non riconosciuta → Near Mint).</summary>
        public List<string> Warnings { get; } = new();
        /// <summary>Se valorizzato la riga non è importabile.</summary>
        public string? Error { get; set; }
    }

    public class CollectionCsvParseResult
    {
        public string DetectedFormat { get; set; } = "Generico";
        /// <summary>Campo riconosciuto → intestazione del file da cui viene letto.</summary>
        public Dictionary<ImportField, string> Mapping { get; set; } = new();
        public List<string> IgnoredColumns { get; set; } = new();
        public List<ParsedCollectionRow> Rows { get; set; } = new();
        public string? Error { get; set; }
    }

    /// <summary>
    /// Interpreta export CSV di collezioni Magic (ManaBox, Moxfield, Deckbox, Dragon Shield, Cardmarket, generici)
    /// riconoscendo le colonne dai nomi più comuni.
    /// </summary>
    public static class CollectionCsvParser
    {
        public const int MaxRows = 5000;

        // Chiavi normalizzate (minuscolo, senza spazi/underscore/trattini)
        private static readonly Dictionary<string, ImportField> HeaderAliases = new()
        {
            ["name"] = ImportField.Name, ["productname"] = ImportField.Name, ["cardname"] = ImportField.Name, ["card"] = ImportField.Name, ["nome"] = ImportField.Name, ["nomecarta"] = ImportField.Name,
            ["setcode"] = ImportField.SetCode, ["setnumber"] = ImportField.SetCode, ["setid"] = ImportField.SetCode, ["editioncode"] = ImportField.SetCode, ["expansioncode"] = ImportField.SetCode, ["codiceset"] = ImportField.SetCode,
            ["setname"] = ImportField.SetName, ["expansion"] = ImportField.SetName, ["expansionname"] = ImportField.SetName, ["editionname"] = ImportField.SetName,
            ["set"] = ImportField.Edition, ["edition"] = ImportField.Edition, ["espansione"] = ImportField.Edition,
            ["collectornumber"] = ImportField.CollectorNumber, ["cardnumber"] = ImportField.CollectorNumber, ["number"] = ImportField.CollectorNumber, ["numero"] = ImportField.CollectorNumber, ["collectorno"] = ImportField.CollectorNumber,
            ["scryfallid"] = ImportField.ScryfallId,
            ["quantity"] = ImportField.Quantity, ["count"] = ImportField.Quantity, ["qty"] = ImportField.Quantity, ["quantita"] = ImportField.Quantity, ["amount"] = ImportField.Quantity,
            ["condition"] = ImportField.Condition, ["cardcondition"] = ImportField.Condition, ["condizione"] = ImportField.Condition,
            ["language"] = ImportField.Language, ["lang"] = ImportField.Language, ["lingua"] = ImportField.Language,
            ["game"] = ImportField.Game, ["gioco"] = ImportField.Game, ["category"] = ImportField.Game, ["categoria"] = ImportField.Game,
            ["grade"] = ImportField.Grade, ["grading"] = ImportField.Grade,
            ["foil"] = ImportField.Finish, ["isfoil"] = ImportField.Finish, ["printing"] = ImportField.Finish, ["finish"] = ImportField.Finish, ["finitura"] = ImportField.Finish,
            ["variance"] = ImportField.Finish, ["variant"] = ImportField.Finish, ["variante"] = ImportField.Finish,
        };

        private static readonly Dictionary<string, CardCondition> ConditionAliases = new()
        {
            ["mint"] = CardCondition.Mint, ["m"] = CardCondition.Mint, ["mt"] = CardCondition.Mint,
            ["nearmint"] = CardCondition.NearMint, ["nm"] = CardCondition.NearMint, ["nmm"] = CardCondition.NearMint,
            ["excellent"] = CardCondition.Excellent, ["ex"] = CardCondition.Excellent, ["slightlyplayed"] = CardCondition.Excellent, ["sp"] = CardCondition.Excellent,
            ["good"] = CardCondition.Good, ["gd"] = CardCondition.Good,
            ["lightplayed"] = CardCondition.LightlyPlayed, ["lightlyplayed"] = CardCondition.LightlyPlayed, ["lp"] = CardCondition.LightlyPlayed,
            ["played"] = CardCondition.ModeratelyPlayed, ["pl"] = CardCondition.ModeratelyPlayed, ["moderatelyplayed"] = CardCondition.ModeratelyPlayed, ["mp"] = CardCondition.ModeratelyPlayed,
            ["heavilyplayed"] = CardCondition.HeavilyPlayed, ["hp"] = CardCondition.HeavilyPlayed,
            ["poor"] = CardCondition.Damaged, ["po"] = CardCondition.Damaged, ["damaged"] = CardCondition.Damaged, ["dmg"] = CardCondition.Damaged,
        };

        private static readonly Dictionary<string, string> LanguageAliases = new()
        {
            ["english"] = "en", ["inglese"] = "en",
            ["italian"] = "it", ["italiano"] = "it",
            ["french"] = "fr", ["francese"] = "fr",
            ["german"] = "de", ["tedesco"] = "de",
            ["spanish"] = "es", ["spagnolo"] = "es", ["sp"] = "es",
            ["portuguese"] = "pt", ["portoghese"] = "pt",
            ["japanese"] = "ja", ["giapponese"] = "ja", ["jp"] = "ja",
            ["korean"] = "ko", ["coreano"] = "ko", ["kr"] = "ko",
            ["russian"] = "ru", ["russo"] = "ru",
            ["chinesesimplified"] = "zhs", ["simplifiedchinese"] = "zhs", ["schinese"] = "zhs", ["cs"] = "zhs", ["zh"] = "zhs", ["zhcn"] = "zhs",
            ["chinesetraditional"] = "zht", ["traditionalchinese"] = "zht", ["tchinese"] = "zht", ["ct"] = "zht", ["zhtw"] = "zht",
        };

        // Codici numerici di lingua usati da Cardmarket
        private static readonly Dictionary<string, string> CardmarketLanguageIds = new()
        {
            ["1"] = "en", ["2"] = "fr", ["3"] = "de", ["4"] = "es", ["5"] = "it",
            ["6"] = "zhs", ["7"] = "ja", ["8"] = "pt", ["9"] = "ru", ["10"] = "ko", ["11"] = "zht",
        };

        private static readonly Regex SetCodePattern = new("^[A-Za-z0-9]{2,6}$", RegexOptions.Compiled);
        private static readonly Regex NameWithNumber = new(@"^(.+?)\s+-\s+#?(\d{1,4}[a-z]?/[A-Za-z0-9]{1,5})$", RegexOptions.Compiled);

        public static CollectionCsvParseResult Parse(string csvText)
        {
            var result = new CollectionCsvParseResult();
            CsvTable table;
            try
            {
                table = CsvTable.Parse(csvText);
            }
            catch (Exception)
            {
                result.Error = "Il file non sembra un CSV valido.";
                return result;
            }

            if (table.Headers.Count == 0)
            {
                result.Error = "Il file è vuoto.";
                return result;
            }

            var columnIndex = new Dictionary<ImportField, int>();
            for (int i = 0; i < table.Headers.Count; i++)
            {
                var key = NormalizeKey(table.Headers[i]);
                if (HeaderAliases.TryGetValue(key, out var field) && !columnIndex.ContainsKey(field))
                {
                    columnIndex[field] = i;
                    result.Mapping[field] = table.Headers[i];
                }
                else
                {
                    result.IgnoredColumns.Add(table.Headers[i]);
                }
            }

            var hasSetAndNumber = (columnIndex.ContainsKey(ImportField.SetCode) || columnIndex.ContainsKey(ImportField.Edition))
                                  && columnIndex.ContainsKey(ImportField.CollectorNumber);
            if (!columnIndex.ContainsKey(ImportField.Name) && !columnIndex.ContainsKey(ImportField.ScryfallId) && !hasSetAndNumber)
            {
                result.Error = "Non trovo la colonna con il nome della carta (es. \"Name\"), né set e numero, né lo Scryfall ID.";
                return result;
            }

            result.DetectedFormat = DetectFormat(table.Headers);
            var isCardmarket = result.DetectedFormat == "Cardmarket";

            if (table.Rows.Count > MaxRows)
            {
                result.Error = $"Il file contiene {table.Rows.Count} righe: il massimo per singolo import è {MaxRows}. Dividilo in più file.";
                return result;
            }

            foreach (var (lineNumber, values) in table.Rows)
            {
                string? Get(ImportField f) =>
                    columnIndex.TryGetValue(f, out var idx) && idx < values.Count && !string.IsNullOrWhiteSpace(values[idx])
                        ? values[idx].Trim()
                        : null;

                var row = new ParsedCollectionRow
                {
                    LineNumber = lineNumber,
                    Name = Get(ImportField.Name),
                    SetCode = Get(ImportField.SetCode)?.ToLowerInvariant(),
                    SetName = Get(ImportField.SetName),
                    CollectorNumber = Get(ImportField.CollectorNumber),
                    ScryfallId = Get(ImportField.ScryfallId)?.ToLowerInvariant(),
                    Game = Get(ImportField.Game),
                };

                // "Set"/"Edition" può essere un codice (neo) o un nome (Kamigawa: Neon Dynasty)
                var edition = Get(ImportField.Edition);
                if (edition != null)
                {
                    if (row.SetCode == null && SetCodePattern.IsMatch(edition)) row.SetCode = edition.ToLowerInvariant();
                    else row.SetName ??= edition;
                }

                if (row.ScryfallId != null && !Guid.TryParse(row.ScryfallId, out _))
                {
                    row.Warnings.Add("Scryfall ID non valido, ignorato");
                    row.ScryfallId = null;
                }

                var qty = Get(ImportField.Quantity);
                if (qty != null)
                {
                    if (int.TryParse(qty, NumberStyles.Integer, CultureInfo.InvariantCulture, out var q) && q > 0) row.Quantity = q;
                    else if (int.TryParse(qty, out q) && q == 0) row.Error = "Quantità 0";
                    else row.Error = $"Quantità non valida: \"{qty}\"";
                }

                var condition = Get(ImportField.Condition);
                if (condition != null)
                {
                    var (c, foilInCondition) = ParseCondition(condition);
                    if (c.HasValue) row.Condition = c.Value;
                    else row.Warnings.Add($"Condizione \"{condition}\" non riconosciuta: impostata Near Mint");
                    if (foilInCondition) row.Finish = CardFinish.Foil;
                }

                // Carte gradate (es. "PSA 10"): si importano come Mint, il voto resta solo nell'avviso
                var grade = Get(ImportField.Grade);
                if (grade != null && !NormalizeKey(grade).StartsWith("ungraded") && NormalizeKey(grade) != "raw")
                {
                    row.Condition = CardCondition.Mint;
                    row.Warnings.Add($"Carta gradata ({grade}): importata come Mint");
                }

                // "Charizard ex - 199/165" (Collectr, TCGplayer): il numero nel nome si usa se manca la colonna del numero
                if (row.Name != null)
                {
                    var m = NameWithNumber.Match(row.Name);
                    if (m.Success)
                    {
                        row.Name = m.Groups[1].Value.Trim();
                        row.CollectorNumber ??= m.Groups[2].Value.Trim();
                    }
                }

                var language = Get(ImportField.Language);
                if (language != null)
                {
                    row.Language = ParseLanguage(language, isCardmarket);
                    if (row.Language == null) row.Warnings.Add($"Lingua \"{language}\" non riconosciuta: lasciata non specificata");
                }

                var finish = Get(ImportField.Finish);
                if (finish != null && row.Finish == CardFinish.Normal) row.Finish = ParseFinish(finish);

                // Senza nome bastano altri riferimenti (set + numero, codice di stampa come LOB-EN001 o OP01-001):
                // decide il catalogo del gioco se sono sufficienti
                if (row.Name == null && row.ScryfallId == null && row.SetCode == null && row.SetName == null && row.CollectorNumber == null)
                    row.Error ??= "Nome della carta mancante";

                result.Rows.Add(row);
            }

            return result;
        }

        public static string? ParseLanguage(string value, bool cardmarketIds = false)
        {
            var trimmed = value.Trim();
            if (cardmarketIds && CardmarketLanguageIds.TryGetValue(trimmed, out var cm)) return cm;
            var direct = CardLanguages.Normalize(trimmed);
            if (direct != null) return direct;
            return LanguageAliases.TryGetValue(NormalizeKey(trimmed), out var alias) ? alias : null;
        }

        public static CardFinish ParseFinish(string value)
        {
            var key = NormalizeKey(value);
            return key switch
            {
                "etched" or "etchedfoil" => CardFinish.Etched,
                "reverseholo" or "reverse" or "reverseholofoil" => CardFinish.ReverseHolo,
                "foil" or "true" or "yes" or "y" or "1" or "si" or "x" or "holo" or "holofoil" => CardFinish.Foil,
                _ when key.Contains("reverse") => CardFinish.ReverseHolo,
                // "1st Edition Holofoil", "Cold Foil", "Enchanted" (Lorcana)
                _ when key.Contains("etched") => CardFinish.Etched,
                _ when key.Contains("foil") || key.Contains("holo") || key == "enchanted" => CardFinish.Foil,
                _ => CardFinish.Normal
            };
        }

        /// <summary>"Near Mint", "NM", "Near Mint Foil" (TCGplayer: la finitura è nella condizione).</summary>
        public static (CardCondition? Condition, bool Foil) ParseCondition(string value)
        {
            var key = NormalizeKey(value);
            if (ConditionAliases.TryGetValue(key, out var c)) return (c, false);
            foreach (var suffix in new[] { "holofoil", "foil", "holo" })
            {
                if (key.EndsWith(suffix) && ConditionAliases.TryGetValue(key[..^suffix.Length], out var c2)) return (c2, true);
            }
            return (null, false);
        }

        private static string DetectFormat(IReadOnlyList<string> headers)
        {
            var keys = headers.Select(NormalizeKey).ToHashSet();
            if (keys.Contains("manaboxid")) return "ManaBox";
            if (keys.Contains("portfolioname") || (keys.Contains("productname") && keys.Contains("category"))) return "Collectr";
            if (keys.Contains("simplename") || (keys.Contains("productid") && keys.Contains("sku"))) return "TCGplayer";
            if (keys.Contains("setnumber") && keys.Contains("cardnumber") && keys.Contains("variant")) return "Dreamborn";
            if (keys.Contains("foldername") && keys.Contains("printing")) return "Dragon Shield";
            if (keys.Contains("idproduct")) return "Cardmarket";
            if (keys.Contains("tradelistcount") && keys.Contains("editioncode")) return "Deckbox";
            if (keys.Contains("tradelistcount")) return "Moxfield";
            return "Generico";
        }

        private static string NormalizeKey(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var ch in value.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue; // toglie accenti
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }
    }
}
