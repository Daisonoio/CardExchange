using System.Globalization;
using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Scryfall;
using CardExchange.Core.Constants;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Import
{
    // ---------------- DTO ----------------

    public class ImportPreviewRequest
    {
        public string Csv { get; set; } = string.Empty;
        /// <summary>Gioco del file (chiave del catalogo). Vuoto o "magic" = Magic tramite Scryfall.</summary>
        public string? CatalogKey { get; set; }
    }

    public class ImportPreviewResponse
    {
        public string DetectedFormat { get; set; } = string.Empty;
        public Dictionary<string, string> Mapping { get; set; } = new();
        public List<string> IgnoredColumns { get; set; } = new();
        public int TotalRows { get; set; }
        public int ReadyCount { get; set; }
        public int ErrorCount { get; set; }
        public int TotalQuantity { get; set; }
        public List<ImportPreviewRow> Rows { get; set; } = new();
    }

    public class ImportPreviewRow
    {
        public int LineNumber { get; set; }
        public string? InputName { get; set; }
        public string? InputSet { get; set; }
        public string? InputNumber { get; set; }
        public int Quantity { get; set; }
        public int Condition { get; set; }
        public string? Language { get; set; }
        public int Finish { get; set; }
        /// <summary>"ready" o "error".</summary>
        public string Status { get; set; } = "ready";
        public string? Error { get; set; }
        public List<string> Warnings { get; set; } = new();
        public ImportMatch? Match { get; set; }
    }

    public class ImportMatch
    {
        public string ScryfallId { get; set; } = string.Empty;
        /// <summary>Per i giochi diversi da Magic: provider e id della carta presso il catalogo.</summary>
        public string? CatalogKey { get; set; }
        public string? ExternalId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SetCode { get; set; } = string.Empty;
        public string SetName { get; set; } = string.Empty;
        public string? CollectorNumber { get; set; }
        public string? ImageSmall { get; set; }
        public decimal? PriceEur { get; set; }
    }

    public class ImportCommitRequest
    {
        public List<ImportCommitRow> Rows { get; set; } = new();
        /// <summary>Se true le carte importate vengono messe subito a disposizione per gli scambi.</summary>
        public bool IsAvailableForTrade { get; set; }
    }

    public class ImportCommitRow
    {
        public string ScryfallId { get; set; } = string.Empty;
        public string? CatalogKey { get; set; }
        public string? ExternalId { get; set; }
        public int Quantity { get; set; } = 1;
        public int Condition { get; set; } = (int)CardCondition.NearMint;
        public string? Language { get; set; }
        public int Finish { get; set; } = (int)CardFinish.Normal;
    }

    public class ImportCommitResponse
    {
        public int CreatedCopies { get; set; }
        public int MergedIntoExisting { get; set; }
        public int TotalQuantity { get; set; }
        public int SkippedByPlanLimit { get; set; }
        public int? PlanCardLimit { get; set; }
        public List<string> Failed { get; set; } = new();
    }

    // ---------------- Servizio ----------------

    public interface ICollectionImportService
    {
        Task<ImportPreviewResponse> PreviewAsync(string csv, string? catalogKey = null);
        Task<ImportCommitResponse> CommitAsync(int userId, ImportCommitRequest request);
    }

    /// <summary>
    /// Import CSV della collezione Magic in due passi: anteprima (nessuna scrittura) e conferma.
    /// Le carte vengono sempre ricondotte alla stampa inglese dello stesso set/numero: la lingua è un attributo
    /// della copia, così i match con le wishlist funzionano qualunque sia la lingua della stampa.
    /// </summary>
    public class CollectionImportService : ICollectionImportService
    {
        public const int MaxCsvLength = 2_000_000;
        private const int ScryfallBatchSize = 75;

        private readonly ApplicationDbContext _context;
        private readonly IScryfallService _scryfall;
        private readonly IMagicCatalogService _catalog;
        private readonly ISubscriptionService _subscriptions;
        private readonly IWishlistAlertService _wishlistAlerts;
        private readonly ILogger<CollectionImportService> _logger;
        private readonly CardExchange.API.Services.Catalog.ICatalogRegistry? _catalogs;

        /// <summary>Righe massime per i giochi risolti carta per carta (le API non hanno ricerche a blocchi).</summary>
        public const int MaxCatalogRows = 1000;

        public CollectionImportService(
            ApplicationDbContext context,
            IScryfallService scryfall,
            IMagicCatalogService catalog,
            ISubscriptionService subscriptions,
            IWishlistAlertService wishlistAlerts,
            ILogger<CollectionImportService> logger,
            CardExchange.API.Services.Catalog.ICatalogRegistry? catalogs = null)
        {
            _catalogs = catalogs;
            _context = context;
            _scryfall = scryfall;
            _catalog = catalog;
            _subscriptions = subscriptions;
            _wishlistAlerts = wishlistAlerts;
            _logger = logger;
        }

        public async Task<ImportPreviewResponse> PreviewAsync(string csv, string? catalogKey = null)
        {
            if (csv.Length > MaxCsvLength)
                throw new ImportException("Il file è troppo grande (massimo 2 MB).");

            var parsed = CollectionCsvParser.Parse(csv);
            if (parsed.Error != null)
                throw new ImportException(parsed.Error);

            // File con la colonna del gioco (Collectr, export di CardExchange…): ogni riga va al suo catalogo,
            // anche se nello stesso file ci sono più giochi
            if (parsed.Mapping.ContainsKey(ImportField.Game) && _catalogs != null
                && parsed.Rows.Any(r => r.Game != null && GameKeyFor(r.Game) is { } k && !k.Equals(catalogKey ?? "magic", StringComparison.OrdinalIgnoreCase)))
                return await PreviewByGameAsync(parsed);

            if (!string.IsNullOrWhiteSpace(catalogKey) && !catalogKey.Equals("magic", StringComparison.OrdinalIgnoreCase))
            {
                var provider = _catalogs?.Get(catalogKey) ?? throw new ImportException("Import non disponibile per questo gioco.");
                return await PreviewWithProviderAsync(parsed, provider);
            }

            return await PreviewMagicAsync(parsed);
        }

        /// <summary>Chiave del catalogo per il gioco scritto nel file ("Pokemon", "One Piece Card Game", "Lorcana", "Magic: The Gathering"…).</summary>
        public string? GameKeyFor(string fileGame)
        {
            if (GameMatches(fileGame, "magic", "Magic: The Gathering") || NormalizeGame(fileGame) is "mtg") return "magic";
            foreach (var p in _catalogs?.All ?? Array.Empty<CardExchange.API.Services.Catalog.ICatalogProvider>())
                if (GameMatches(fileGame, p.Key, p.GameName)) return p.Key;
            var g = NormalizeGame(fileGame);
            if (g.Contains("yugioh")) return _catalogs?.Get("yugioh")?.Key;
            return null;
        }

        private async Task<ImportPreviewResponse> PreviewByGameAsync(CollectionCsvParseResult parsed)
        {
            var groups = parsed.Rows.GroupBy(r => r.Error != null ? "" : r.Game == null ? "magic" : GameKeyFor(r.Game) ?? "?").ToList();
            var parts = new List<ImportPreviewResponse>();
            foreach (var g in groups)
            {
                var sub = new CollectionCsvParseResult
                {
                    DetectedFormat = parsed.DetectedFormat, Mapping = parsed.Mapping, IgnoredColumns = parsed.IgnoredColumns, Rows = g.ToList()
                };
                if (g.Key == "?")
                    foreach (var r in sub.Rows) r.Error = $"Gioco non supportato: {r.Game}";
                if (g.Key is "" or "?" || g.Key == "magic")
                {
                    // Le righe con errore passano dal percorso Magic, che le riporta così come sono
                    if (g.Key == "magic") foreach (var r in sub.Rows) r.Game = null;
                    try
                    {
                        parts.Add(await PreviewMagicAsync(sub));
                    }
                    catch (ImportException ex)
                    {
                        // Scryfall non raggiungibile: le altre carte del file si importano comunque
                        foreach (var r in sub.Rows) r.Error ??= ex.Message;
                        parts.Add(new ImportPreviewResponse
                        {
                            Rows = sub.Rows.Select(r => new ImportPreviewRow
                            {
                                LineNumber = r.LineNumber, InputName = r.Name, InputSet = r.SetCode ?? r.SetName, InputNumber = r.CollectorNumber,
                                Quantity = r.Quantity, Condition = (int)r.Condition, Language = r.Language, Finish = (int)r.Finish,
                                Warnings = r.Warnings, Status = "error", Error = r.Error
                            }).ToList()
                        });
                    }
                }
                else
                {
                    parts.Add(await PreviewWithProviderAsync(sub, _catalogs!.Get(g.Key)!));
                }
            }
            var rows = parts.SelectMany(p => p.Rows).OrderBy(r => r.LineNumber).ToList();
            return new ImportPreviewResponse
            {
                DetectedFormat = parsed.DetectedFormat,
                Mapping = parsed.Mapping.ToDictionary(m => m.Key.ToString(), m => m.Value),
                IgnoredColumns = parsed.IgnoredColumns,
                TotalRows = parsed.Rows.Count,
                Rows = rows,
                ReadyCount = rows.Count(r => r.Status == "ready"),
                ErrorCount = rows.Count(r => r.Status != "ready"),
                TotalQuantity = rows.Where(r => r.Status == "ready").Sum(r => r.Quantity)
            };
        }

        private async Task<ImportPreviewResponse> PreviewMagicAsync(CollectionCsvParseResult parsed)
        {

            // File con la colonna del gioco (es. export di CardExchange): le righe di altri giochi si segnalano
            foreach (var row in parsed.Rows.Where(r => r.Error == null && r.Game != null && !GameMatches(r.Game, "magic", "Magic: The Gathering")))
                row.Error = $"Carta di un altro gioco ({row.Game}): importala scegliendo quel gioco";

            var response = new ImportPreviewResponse
            {
                DetectedFormat = parsed.DetectedFormat,
                Mapping = parsed.Mapping.ToDictionary(m => m.Key.ToString(), m => m.Value),
                IgnoredColumns = parsed.IgnoredColumns,
                TotalRows = parsed.Rows.Count
            };

            var resolvable = parsed.Rows.Where(r => r.Error == null).ToList();
            await FillSetCodesFromNamesAsync(resolvable);
            var resolved = await ResolveAsync(resolvable);

            foreach (var row in parsed.Rows)
            {
                var preview = new ImportPreviewRow
                {
                    LineNumber = row.LineNumber,
                    InputName = row.Name,
                    InputSet = row.SetCode ?? row.SetName,
                    InputNumber = row.CollectorNumber,
                    Quantity = row.Quantity,
                    Condition = (int)row.Condition,
                    Language = row.Language,
                    Finish = (int)row.Finish,
                    Warnings = row.Warnings
                };

                if (row.Error != null)
                {
                    preview.Status = "error";
                    preview.Error = row.Error;
                }
                else if (resolved.TryGetValue(row, out var res) && res.Card != null)
                {
                    var card = res.Card;
                    preview.Match = new ImportMatch
                    {
                        ScryfallId = card.Id,
                        Name = card.Name,
                        SetCode = card.SetCode,
                        SetName = card.SetName,
                        CollectorNumber = card.CollectorNumber,
                        ImageSmall = card.ImageUris?.Small ?? card.CardFaces?.FirstOrDefault()?.ImageUris?.Small,
                        PriceEur = ParsePrice(row.Finish == CardFinish.Normal ? card.Prices?.Eur : card.Prices?.EurFoil ?? card.Prices?.Eur)
                    };
                    if (preview.Language == null && res.PrintLanguage != null)
                        preview.Language = res.PrintLanguage;
                    if (res.EditionGuessed)
                        preview.Warnings.Add($"Edizione non indicata: usata {card.SetName} ({card.SetCode.ToUpperInvariant()})");
                }
                else
                {
                    preview.Status = "error";
                    preview.Error = "Carta non trovata su Scryfall";
                }

                response.Rows.Add(preview);
            }

            response.ReadyCount = response.Rows.Count(r => r.Status == "ready");
            response.ErrorCount = response.Rows.Count - response.ReadyCount;
            response.TotalQuantity = response.Rows.Where(r => r.Status == "ready").Sum(r => r.Quantity);
            return response;
        }

        public async Task<ImportCommitResponse> CommitAsync(int userId, ImportCommitRequest request)
        {
            if (request.Rows.Count == 0)
                throw new ImportException("Nessuna carta da importare.");
            if (request.Rows.Count > CollectionCsvParser.MaxRows)
                throw new ImportException($"Massimo {CollectionCsvParser.MaxRows} righe per import.");

            var response = new ImportCommitResponse();

            // Normalizza e valida le righe ricevute dal client
            var rows = new List<ImportCommitRow>();
            foreach (var r in request.Rows)
            {
                var catalog = IsCatalogRow(r);
                var idOk = catalog
                    ? !string.IsNullOrWhiteSpace(r.ExternalId) && r.ExternalId.Length <= 100 && _catalogs?.Get(r.CatalogKey) != null
                    : Guid.TryParse(r.ScryfallId, out _);
                if (!idOk || r.Quantity < 1 || r.Quantity > 9999
                    || !Enum.IsDefined(typeof(CardCondition), r.Condition) || !Enum.IsDefined(typeof(CardFinish), r.Finish))
                {
                    response.Failed.Add($"Riga non valida ({(catalog ? r.ExternalId : r.ScryfallId)})");
                    continue;
                }
                if (!catalog) r.ScryfallId = r.ScryfallId.ToLowerInvariant();
                r.Language = CardLanguages.Normalize(r.Language);
                rows.Add(r);
            }

            // Altri giochi: schede create o aggiornate tramite il provider (verifica server-side degli id)
            var catalogInfos = new Dictionary<string, CardInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in rows.Where(IsCatalogRow).Select(r => (r.CatalogKey!, r.ExternalId!.Trim())).Distinct())
            {
                var provider = _catalogs!.Get(key.Item1)!;
                try
                {
                    var imported = await provider.ImportAsync(key.Item2);
                    var info = imported == null ? null : await _context.CardInfos.FindAsync(imported.CardInfoId);
                    if (info != null) catalogInfos[$"{key.Item1}|{key.Item2}"] = info;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
                {
                    _logger.LogWarning(ex, "Import {Game}: carta {Id} non importata", provider.GameName, key.Item2);
                }
            }

            // Recupera da Scryfall le carte indicate (verifica server-side degli ID)
            var ids = rows.Where(r => !IsCatalogRow(r)).Select(r => r.ScryfallId).Distinct().ToList();
            var cardsById = new Dictionary<string, ScryfallCard>(StringComparer.OrdinalIgnoreCase);
            foreach (var chunk in ids.Chunk(ScryfallBatchSize))
            {
                var result = await _scryfall.ValidateCollectionAsync(chunk.Select(id => new ScryfallIdentifier { Id = id }).ToList())
                    ?? throw new ImportException("Scryfall non risponde, riprova tra qualche minuto.");
                foreach (var c in result.Data) cardsById[c.Id] = c;
            }

            var plan = await _subscriptions.GetUserActivePlanAsync(userId);
            var maxCards = plan?.MaxCards ?? int.MaxValue;
            response.PlanCardLimit = maxCards == int.MaxValue ? null : maxCards;

            var newlyAvailable = new List<Card>();
            var failedBefore = response.Failed.Count;

            // Transazione compatibile con la ripetizione automatica di SQL Server: se il blocco viene ripetuto
            // riparte da zero (contatori, elenchi e carte già presenti ricaricate dal database)
            await _context.InTransactionAsync(async () =>
            {
            response.CreatedCopies = 0;
            response.MergedIntoExisting = 0;
            response.SkippedByPlanLimit = 0;
            response.TotalQuantity = 0;
            response.Failed.RemoveRange(failedBefore, response.Failed.Count - failedBefore);
            newlyAvailable.Clear();

            var existingCopies = await _context.Cards.Where(c => c.UserId == userId).ToListAsync();
            var copyCount = existingCopies.Count;
            var cardInfoIds = new Dictionary<string, CardInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                CardInfo? cardInfo;
                if (IsCatalogRow(row))
                {
                    if (!catalogInfos.TryGetValue($"{row.CatalogKey}|{row.ExternalId!.Trim()}", out cardInfo))
                    {
                        response.Failed.Add($"Carta non trovata nel catalogo ({row.ExternalId})");
                        continue;
                    }
                }
                else
                {
                    if (!cardsById.TryGetValue(row.ScryfallId, out var scryfallCard))
                    {
                        response.Failed.Add($"Carta non trovata su Scryfall ({row.ScryfallId})");
                        continue;
                    }
                    if (!cardInfoIds.TryGetValue(row.ScryfallId, out cardInfo))
                    {
                        cardInfo = (await _catalog.GetOrCreateCardInfoAsync(scryfallCard)).CardInfo;
                        cardInfoIds[row.ScryfallId] = cardInfo;
                    }
                }

                var condition = (CardCondition)row.Condition;
                var finish = (CardFinish)row.Finish;

                // Stessa carta, condizione, lingua e finitura: si somma alla copia esistente
                var existing = existingCopies.FirstOrDefault(c =>
                    c.CardInfoId == cardInfo.Id && c.Condition == condition && c.Language == row.Language && c.Finish == finish);

                if (existing != null)
                {
                    existing.Quantity += row.Quantity;
                    if (request.IsAvailableForTrade && !existing.IsAvailableForTrade)
                    {
                        existing.IsAvailableForTrade = true;
                        newlyAvailable.Add(existing);
                    }
                    response.MergedIntoExisting++;
                }
                else
                {
                    if (copyCount >= maxCards)
                    {
                        response.SkippedByPlanLimit++;
                        continue;
                    }

                    var card = new Card
                    {
                        UserId = userId,
                        CardInfoId = cardInfo.Id,
                        Condition = condition,
                        Language = row.Language,
                        Finish = finish,
                        Quantity = row.Quantity,
                        IsAvailableForTrade = request.IsAvailableForTrade,
                        EstimatedValue = CardPricing.ReferencePriceEur(cardInfo, finish)
                    };
                    _context.Cards.Add(card);
                    existingCopies.Add(card);
                    if (card.IsAvailableForTrade) newlyAvailable.Add(card);
                    copyCount++;
                    response.CreatedCopies++;
                }
                response.TotalQuantity += row.Quantity;
            }

            await _context.SaveChangesAsync();
            });

            // Dopo il commit le nuove copie hanno un Id
            await _wishlistAlerts.NotifyAvailableAsync(newlyAvailable.Select(c => c.Id));

            _logger.LogInformation("Import collezione utente {UserId}: {Created} nuove copie, {Merged} unite, {Skipped} oltre il limite, {Failed} fallite",
                userId, response.CreatedCopies, response.MergedIntoExisting, response.SkippedByPlanLimit, response.Failed.Count);
            return response;
        }

        // ---------------- Altri giochi: risoluzione tramite provider ----------------

        /// <summary>Una colonna "Set" breve e senza spazi (es. "1", "OP01", "sv3pt5") è un codice, non un nome.</summary>
        private static string? ShortCode(string? setName) =>
            setName != null && setName.Length <= 8 && !setName.Contains(' ') ? setName : null;

        private static bool IsCatalogRow(ImportCommitRow r) =>
            !string.IsNullOrWhiteSpace(r.CatalogKey) && !r.CatalogKey.Equals("magic", StringComparison.OrdinalIgnoreCase);

        private static string NormalizeGame(string s) =>
            new string(s.Normalize(System.Text.NormalizationForm.FormD)
                .Where(ch => char.IsLetterOrDigit(ch)).Select(char.ToLowerInvariant).ToArray());

        /// <summary>"Pokémon TCG", "pokemon", "Disney Lorcana", "Yu-Gi-Oh!" riconosciuti dalla chiave o dal nome del gioco.</summary>
        public static bool GameMatches(string fileGame, string key, string gameName)
        {
            var g = NormalizeGame(fileGame);
            var k = NormalizeGame(key);
            return g.Length > 0 && (g.Contains(k) || NormalizeGame(gameName).Contains(g));
        }

        private async Task<ImportPreviewResponse> PreviewWithProviderAsync(CollectionCsvParseResult parsed, CardExchange.API.Services.Catalog.ICatalogProvider provider)
        {
            if (parsed.Rows.Count > MaxCatalogRows)
                throw new ImportException($"Per {provider.GameName} il massimo è {MaxCatalogRows} righe per import: dividi il file.");

            foreach (var row in parsed.Rows.Where(r => r.Error == null && r.Game != null && !GameMatches(r.Game, provider.Key, provider.GameName)))
                row.Error = $"Carta di un altro gioco ({row.Game}): importala scegliendo quel gioco";

            static string LookupKey(ParsedCollectionRow r) =>
                string.Join("|", new[] { r.Name, r.SetCode, r.SetName, r.CollectorNumber }.Select(x => x?.Trim().ToLowerInvariant() ?? ""));

            var lookups = parsed.Rows.Where(r => r.Error == null)
                .GroupBy(LookupKey)
                .ToDictionary(g => g.Key, g => new CardExchange.API.Services.Catalog.CatalogLookup(
                    g.First().Name, g.First().SetCode ?? ShortCode(g.First().SetName), g.First().SetName, g.First().CollectorNumber));

            // Le API dei cataloghi rispondono una carta alla volta: poche richieste in parallelo, nel rispetto dei limiti
            var resolved = new System.Collections.Concurrent.ConcurrentDictionary<string, CardExchange.API.Services.Catalog.CatalogCard?>();
            using var gate = new SemaphoreSlim(3);
            await Task.WhenAll(lookups.Select(async kv =>
            {
                await gate.WaitAsync();
                try
                {
                    resolved[kv.Key] = await provider.ResolveAsync(kv.Value);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
                {
                    _logger.LogWarning(ex, "Import {Game}: riga non risolta", provider.GameName);
                    resolved[kv.Key] = null;
                }
                finally
                {
                    gate.Release();
                }
            }));

            var response = new ImportPreviewResponse
            {
                DetectedFormat = parsed.DetectedFormat,
                Mapping = parsed.Mapping.ToDictionary(m => m.Key.ToString(), m => m.Value),
                IgnoredColumns = parsed.IgnoredColumns,
                TotalRows = parsed.Rows.Count
            };
            foreach (var row in parsed.Rows)
            {
                var preview = new ImportPreviewRow
                {
                    LineNumber = row.LineNumber,
                    InputName = row.Name,
                    InputSet = row.SetCode ?? row.SetName,
                    InputNumber = row.CollectorNumber,
                    Quantity = row.Quantity,
                    Condition = (int)row.Condition,
                    Language = row.Language,
                    Finish = (int)row.Finish,
                    Warnings = row.Warnings
                };
                if (row.Error != null)
                {
                    preview.Status = "error";
                    preview.Error = row.Error;
                }
                else if (resolved.TryGetValue(LookupKey(row), out var card) && card != null)
                {
                    preview.Match = new ImportMatch
                    {
                        CatalogKey = provider.Key,
                        ExternalId = card.ExternalId,
                        Name = card.Name,
                        SetCode = card.SetCode ?? "",
                        SetName = card.SetName ?? "",
                        CollectorNumber = card.Number,
                        ImageSmall = card.ImageSmall,
                        PriceEur = row.Finish != CardFinish.Normal && card.PriceEurFoil.HasValue ? card.PriceEurFoil : card.PriceEur
                    };
                    if (row.SetCode == null && row.CollectorNumber == null && card.SetName != null)
                        preview.Warnings.Add($"Edizione non indicata: usata {card.SetName}");
                }
                else
                {
                    preview.Status = "error";
                    preview.Error = $"Carta non trovata nel catalogo {provider.GameName} (indica {provider.ImportHint})";
                }
                response.Rows.Add(preview);
            }
            response.ReadyCount = response.Rows.Count(r => r.Status == "ready");
            response.ErrorCount = response.Rows.Count - response.ReadyCount;
            response.TotalQuantity = response.Rows.Where(r => r.Status == "ready").Sum(r => r.Quantity);
            return response;
        }

        // ---------------- Risoluzione su Scryfall ----------------

        private record Resolution(ScryfallCard? Card, bool EditionGuessed, string? PrintLanguage);

        /// <summary>"Magic 2010" → m10: con il nome dell'espansione (Collectr, TCGplayer) si trova la stampa giusta invece di indovinarla.</summary>
        private async Task FillSetCodesFromNamesAsync(List<ParsedCollectionRow> rows)
        {
            var needing = rows.Where(r => r.SetCode == null && r.SetName != null && r.ScryfallId == null).ToList();
            if (needing.Count == 0) return;
            List<ScryfallSet> sets;
            try
            {
                sets = await _scryfall.GetAllSetsAsync() ?? new List<ScryfallSet>();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return;
            }
            static string Simple(string s) => new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            var byName = sets.Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Code))
                .GroupBy(x => Simple(x.Name)).ToDictionary(g => g.Key, g => g.First().Code.ToLowerInvariant());
            foreach (var r in needing)
                if (byName.TryGetValue(Simple(r.SetName!), out var code)) r.SetCode = code;
        }

        private static string? KeyFor(ParsedCollectionRow r)
        {
            if (r.SetCode != null && r.CollectorNumber != null) return $"sn:{r.SetCode}|{r.CollectorNumber.ToLowerInvariant()}";
            if (r.ScryfallId != null) return $"id:{r.ScryfallId}";
            if (r.Name != null && r.SetCode != null) return $"ns:{r.Name.ToLowerInvariant()}|{r.SetCode}";
            if (r.Name != null) return $"n:{r.Name.ToLowerInvariant()}";
            return null;
        }

        private static ScryfallIdentifier IdentifierFor(string key)
        {
            var (kind, value) = (key[..key.IndexOf(':')], key[(key.IndexOf(':') + 1)..]);
            var parts = value.Split('|');
            return kind switch
            {
                "sn" => new ScryfallIdentifier { Set = parts[0], CollectorNumber = parts[1] },
                "id" => new ScryfallIdentifier { Id = value },
                "ns" => new ScryfallIdentifier { Name = parts[0], Set = parts[1] },
                _ => new ScryfallIdentifier { Name = value }
            };
        }

        private static IEnumerable<string> KeysOf(ScryfallCard c)
        {
            var set = c.SetCode.ToLowerInvariant();
            yield return $"id:{c.Id.ToLowerInvariant()}";
            if (c.CollectorNumber != null) yield return $"sn:{set}|{c.CollectorNumber.ToLowerInvariant()}";
            var names = new List<string> { c.Name };
            if (c.Name.Contains(" // ")) names.AddRange(c.Name.Split(" // "));
            if (c.CardFaces != null) names.AddRange(c.CardFaces.Where(f => f.Name != null).Select(f => f.Name!));
            foreach (var n in names.Select(n => n.ToLowerInvariant()).Distinct())
            {
                yield return $"ns:{n}|{set}";
                yield return $"n:{n}";
            }
        }

        private async Task<Dictionary<ParsedCollectionRow, Resolution>> ResolveAsync(List<ParsedCollectionRow> rows)
        {
            var keyByRow = rows.Select(r => (Row: r, Key: KeyFor(r))).Where(x => x.Key != null).ToList();
            var found = await LookupAsync(keyByRow.Select(x => x.Key!).Distinct().ToList());

            // Stampe non inglesi (es. Scryfall ID di una stampa italiana): si passa alla stampa inglese dello stesso set/numero
            var foreign = found.Values.Where(c => c.Lang != null && c.Lang != "en" && c.CollectorNumber != null)
                .Select(c => $"sn:{c.SetCode.ToLowerInvariant()}|{c.CollectorNumber!.ToLowerInvariant()}")
                .Distinct().ToList();
            var english = foreign.Count > 0 ? await LookupAsync(foreign) : new Dictionary<string, ScryfallCard>();

            var result = new Dictionary<ParsedCollectionRow, Resolution>();
            foreach (var (row, key) in keyByRow)
            {
                if (!found.TryGetValue(key!, out var card)) continue;
                string? printLanguage = null;
                if (card.Lang != null && card.Lang != "en")
                {
                    printLanguage = CardLanguages.Normalize(card.Lang);
                    var enKey = $"sn:{card.SetCode.ToLowerInvariant()}|{card.CollectorNumber?.ToLowerInvariant()}";
                    if (english.TryGetValue(enKey, out var en) && en.Lang == "en") card = en;
                }
                result[row] = new Resolution(card, EditionGuessed: row.SetCode == null && row.ScryfallId == null, printLanguage);
            }
            return result;
        }

        private async Task<Dictionary<string, ScryfallCard>> LookupAsync(List<string> keys)
        {
            var found = new Dictionary<string, ScryfallCard>();
            foreach (var chunk in keys.Chunk(ScryfallBatchSize))
            {
                var response = await _scryfall.ValidateCollectionAsync(chunk.Select(IdentifierFor).ToList())
                    ?? throw new ImportException("Scryfall non risponde, riprova tra qualche minuto.");
                var wanted = chunk.ToHashSet();
                foreach (var card in response.Data)
                    foreach (var k in KeysOf(card).Where(wanted.Contains))
                        found.TryAdd(k, card);
            }
            return found;
        }

        private static decimal? ParsePrice(string? value) =>
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : null;
    }

    /// <summary>Errore comprensibile dall'utente durante l'import.</summary>
    public class ImportException : Exception
    {
        public ImportException(string message) : base(message) { }
    }
}
