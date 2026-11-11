using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Prices
{
    public class DailyMaintenanceOptions
    {
        public bool Enabled { get; set; } = true;
        /// <summary>Ora di esecuzione (UTC), formato HH:mm.</summary>
        public string RunAtUtc { get; set; } = "02:30";
        /// <summary>Esegue anche all'avvio dell'applicazione (utile in sviluppo).</summary>
        public bool RunOnStartup { get; set; }
        /// <summary>Limite di carte Pokémon aggiornate per notte (l'API pubblica ha una quota giornaliera).</summary>
        public int MaxPokemonPerRun { get; set; } = 500;
        /// <summary>Limite di carte Yu-Gi-Oh! aggiornate per notte.</summary>
        public int MaxYuGiOhPerRun { get; set; } = 1000;
        /// <summary>Limite di carte per notte per ciascun gioco gestito da un provider generico (es. Lorcana).</summary>
        public int MaxCatalogPerRun { get; set; } = 1000;
    }

    public record DailyMaintenanceReport(int ExpiredOffers, int MagicUpdated, int PokemonUpdated, int YuGiOhUpdated, int Snapshots, int UsersWithAlertsChecked);

    /// <summary>
    /// Lavori notturni: chiude le offerte scadute, aggiorna i prezzi delle carte presenti in collezioni
    /// o wishlist, registra lo storico prezzi giornaliero e valuta gli allarmi prezzo.
    /// Ogni passo è idempotente: rieseguirlo nello stesso giorno non crea duplicati.
    /// </summary>
    public class DailyMaintenance
    {
        private readonly ApplicationDbContext _context;
        private readonly ICatalogPriceRefresher _refresher;
        private readonly IPriceTrackingService _priceTracking;
        private readonly ILogger<DailyMaintenance> _logger;
        private readonly CardExchange.API.Services.Catalog.ICatalogRegistry? _catalog;

        public DailyMaintenance(ApplicationDbContext context, ICatalogPriceRefresher refresher,
            IPriceTrackingService priceTracking, ILogger<DailyMaintenance> logger,
            CardExchange.API.Services.Catalog.ICatalogRegistry? catalog = null)
        {
            _catalog = catalog;
            _context = context;
            _refresher = refresher;
            _priceTracking = priceTracking;
            _logger = logger;
        }

        public async Task<DailyMaintenanceReport> RunAsync(DailyMaintenanceOptions options, CancellationToken ct = default)
        {
            var expired = await ExpireOffersAsync(ct);
            var (magic, pokemon, yugioh) = await RefreshPricesAsync(options, ct);
            await RefreshCatalogProvidersAsync(options, ct);
            var snapshots = await SnapshotPricesAsync(ct);
            var alertUsers = await CheckPriceAlertsAsync(ct);

            var report = new DailyMaintenanceReport(expired, magic, pokemon, yugioh, snapshots, alertUsers);
            _logger.LogInformation("Manutenzione notturna completata: {@Report}", report);
            return report;
        }

        public async Task<int> ExpireOffersAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var offers = await _context.TradeOffers
                .Where(o => o.Status == TradeOfferStatus.Pending && o.ExpiresAt != null && o.ExpiresAt < now)
                .ToListAsync(ct);
            foreach (var offer in offers)
            {
                offer.Status = TradeOfferStatus.Expired;
                offer.ResponseDate = now;
            }
            await _context.SaveChangesAsync(ct);
            return offers.Count;
        }

        /// <summary>CardInfo in uso: almeno una copia in collezione o una voce di wishlist.</summary>
        private IQueryable<CardInfo> CardInfosInUse() =>
            _context.CardInfos.Where(ci =>
                _context.Cards.Any(c => c.CardInfoId == ci.Id) ||
                _context.WishlistItems.Any(w => w.CardInfoId == ci.Id));

        public async Task<(int Magic, int Pokemon, int YuGiOh)> RefreshPricesAsync(DailyMaintenanceOptions options, CancellationToken ct = default)
        {
            int magic = 0, pokemon = 0, yugioh = 0;

            try
            {
                var magicInfos = await CardInfosInUse().Where(ci => ci.ScryfallId != null).ToListAsync(ct);
                magic = await _refresher.RefreshMagicAsync(magicInfos, ct);
                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Aggiornamento prezzi Magic non riuscito");
            }

            try
            {
                // Prima le più vecchie, così con la quota giornaliera si ruota su tutto il catalogo
                var pokemonInfos = await CardInfosInUse().Where(ci => ci.PokemonTcgId != null)
                    .OrderBy(ci => ci.PokemonTcgUpdatedAt).Take(options.MaxPokemonPerRun).ToListAsync(ct);
                pokemon = await _refresher.RefreshPokemonAsync(pokemonInfos, ct);
                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Aggiornamento prezzi Pokémon non riuscito");
            }

            try
            {
                var yugiohInfos = await CardInfosInUse().Where(ci => ci.YuGiOhId != null)
                    .OrderBy(ci => ci.YuGiOhUpdatedAt).Take(options.MaxYuGiOhPerRun).ToListAsync(ct);
                yugioh = await _refresher.RefreshYuGiOhAsync(yugiohInfos, ct);
                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Aggiornamento prezzi Yu-Gi-Oh! non riuscito");
            }

            return (magic, pokemon, yugioh);
        }

        /// <summary>Giochi dei provider generici (es. Lorcana): prima le schede aggiornate da più tempo.</summary>
        public async Task<int> RefreshCatalogProvidersAsync(DailyMaintenanceOptions options, CancellationToken ct = default)
        {
            if (_catalog == null) return 0;
            var total = 0;
            foreach (var provider in _catalog.All.OfType<CardExchange.API.Services.Catalog.GenericCatalogProvider>())
            {
                try
                {
                    var key = provider.Key;
                    var infos = await CardInfosInUse().Where(ci => ci.CatalogSource == key)
                        .OrderBy(ci => ci.CatalogUpdatedAt).Take(options.MaxCatalogPerRun).ToListAsync(ct);
                    var updated = await provider.RefreshAsync(infos, ct);
                    await _context.SaveChangesAsync(ct);
                    _logger.LogInformation("Prezzi {Game}: {Updated} carte aggiornate", provider.GameName, updated);
                    total += updated;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Aggiornamento prezzi {Game} non riuscito", provider.GameName);
                }
            }
            return total;
        }

        /// <summary>Una riga di storico per carta in uso e per giorno (aggiornata se già presente).</summary>
        public async Task<int> SnapshotPricesAsync(CancellationToken ct = default)
        {
            var today = DateTime.UtcNow.Date;
            var infos = await CardInfosInUse()
                .Where(ci => ci.PriceEur != null || ci.PriceUsd != null || ci.PriceEurFoil != null || ci.PriceUsdFoil != null)
                .Select(ci => new { ci.Id, ci.PriceUsd, ci.PriceUsdFoil, ci.PriceEur, ci.PriceEurFoil })
                .ToListAsync(ct);

            var existing = await _context.PriceHistories
                .Where(ph => ph.SnapshotDate == today)
                .ToDictionaryAsync(ph => ph.CardInfoId, ct);

            foreach (var info in infos)
            {
                if (existing.TryGetValue(info.Id, out var snap))
                {
                    snap.PriceUsd = info.PriceUsd;
                    snap.PriceUsdFoil = info.PriceUsdFoil;
                    snap.PriceEur = info.PriceEur;
                    snap.PriceEurFoil = info.PriceEurFoil;
                }
                else
                {
                    _context.PriceHistories.Add(new PriceHistory
                    {
                        CardInfoId = info.Id,
                        SnapshotDate = today,
                        PriceUsd = info.PriceUsd,
                        PriceUsdFoil = info.PriceUsdFoil,
                        PriceEur = info.PriceEur,
                        PriceEurFoil = info.PriceEurFoil
                    });
                }
            }
            await _context.SaveChangesAsync(ct);
            return infos.Count;
        }

        public async Task<int> CheckPriceAlertsAsync(CancellationToken ct = default)
        {
            var userIds = await _context.PriceAlerts
                .Where(a => a.IsActive && !a.IsTriggered)
                .Select(a => a.UserId).Distinct().ToListAsync(ct);
            foreach (var userId in userIds)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await _priceTracking.CheckAndTriggerAlertsAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Verifica allarmi prezzo non riuscita per utente {UserId}", userId);
                }
            }
            return userIds.Count;
        }
    }

    /// <summary>
    /// Pianifica <see cref="DailyMaintenance"/> una volta al giorno all'ora configurata (sezione Jobs:DailyMaintenance).
    /// Con più istanze dell'API il lavoro viene ripetuto su ciascuna, ma resta corretto perché idempotente.
    /// </summary>
    public class DailyMaintenanceHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DailyMaintenanceHostedService> _logger;

        public DailyMaintenanceHostedService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<DailyMaintenanceHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var options = _configuration.GetSection("Jobs:DailyMaintenance").Get<DailyMaintenanceOptions>() ?? new DailyMaintenanceOptions();
            if (!options.Enabled)
            {
                _logger.LogInformation("Manutenzione notturna disattivata da configurazione");
                return;
            }

            if (options.RunOnStartup)
                await RunOnceAsync(options, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = DelayUntilNextRun(options.RunAtUtc, DateTime.UtcNow);
                _logger.LogInformation("Prossima manutenzione notturna tra {Delay}", delay);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                await RunOnceAsync(options, stoppingToken);
            }
        }

        private async Task RunOnceAsync(DailyMaintenanceOptions options, CancellationToken ct)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    await scope.ServiceProvider.GetRequiredService<DailyMaintenance>().RunAsync(options, ct);
                }

                using (var billingScope = _scopeFactory.CreateScope())
                {
                    var (expired, reminded, stale) = await billingScope.ServiceProvider
                        .GetRequiredService<CardExchange.API.Services.Billing.IBillingService>().RunMaintenanceAsync(DateTime.UtcNow);
                    _logger.LogInformation("Abbonamenti: {Expired} scaduti, {Reminded} promemoria, {Stale} richieste di bonifico annullate",
                        expired, reminded, stale);
                }

                // Copia locale del catalogo del gioco di punta (Pokémon): ricerca e import anche con l'API ferma
                if (_configuration.GetValue("Jobs:CatalogMirror:Enabled", true))
                {
                    using var mirrorScope = _scopeFactory.CreateScope();
                    await mirrorScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.Catalog.PokemonCatalogMirror>().RunAsync(DateTime.UtcNow, ct);
                }

                // Crediti promozione dei piani (Banco, Negozio Pro): una volta per mese di calendario
                using (var creditScope = _scopeFactory.CreateScope())
                    await creditScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.Billing.PromotionService>().GrantMonthlyCreditsAsync(DateTime.UtcNow, ct);

                // Impronte visive per lo scanner: le carte nuove del catalogo (a blocchi)
                if (_configuration.GetValue("Jobs:ScanIndex:Enabled", true))
                {
                    using var scanScope = _scopeFactory.CreateScope();
                    await scanScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.Scan.CardFingerprintIndexer>()
                        .RunAsync(_configuration.GetValue("Jobs:ScanIndex:BatchSize", 1500), DateTime.UtcNow, ct);
                }

                // Immagini One Piece: se è configurata una sorgente (diritti verificati), il catalogo già salvato passa a quella
                if (CardExchange.API.Services.Catalog.OnePieceImageSource.Template(_configuration) is { } opTemplate)
                {
                    using var opScope = _scopeFactory.CreateScope();
                    var changed = await CardExchange.API.Services.Catalog.OnePieceImageSource.BackfillAsync(
                        opScope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), opTemplate, ct: ct);
                    if (changed > 0) _logger.LogInformation("Immagini One Piece aggiornate alla nuova sorgente: {Count}", changed);
                }

                // Commissioni ai negozi: maturano dopo 30 giorni; il primo del mese si liquidano come sconto sul piano
                using (var commissionScope = _scopeFactory.CreateScope())
                {
                    var sp = commissionScope.ServiceProvider;
                    var commissions = sp.GetRequiredService<CardExchange.API.Services.Billing.StoreCommissions>();
                    var now = DateTime.UtcNow;
                    if (CardExchange.Core.Rules.TimeZones.ToLocal(now, null).Day == 1
                        && !await sp.GetRequiredService<ApplicationDbContext>().StorePayouts.AnyAsync(p => p.CreatedAt >= now.AddDays(-2), ct))
                        await commissions.PayoutAsync(now, sp.GetRequiredService<CardExchange.API.Services.Billing.IStripeBilling>(), ct);
                    else
                        await commissions.ApproveAsync(now, ct);
                }

                // Inviti con premio: nuovi invitati attivi e mesi Premium guadagnati (solo con i piani a pagamento accesi)
                if (_configuration.GetValue("Features:PaidPlans", false))
                using (var referralScope = _scopeFactory.CreateScope())
                {
                    await referralScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.ReferralRewards>().RunAsync(DateTime.UtcNow, ct);
                }

                // Lista acquisti dei negozi: avvisi settimanali a chi ha le carte richieste
                using (var buyScope = _scopeFactory.CreateScope())
                {
                    var sp = buyScope.ServiceProvider;
                    await sp.GetRequiredService<CardExchange.API.Services.Stores.BuyListService>()
                        .SendAlertsAsync(DateTime.UtcNow, sp.GetRequiredService<CardExchange.API.Services.Notifications.INotificationDispatcher>(), ct);
                }

                // Domanda locale dei negozi
                using (var demandScope = _scopeFactory.CreateScope())
                {
                    await demandScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.Stores.StoreDemandService>().RefreshAllAsync(DateTime.UtcNow, ct);
                }

                // Riepilogo mensile dei negozi (crea solo quelli del mese appena concluso non ancora fatti)
                using (var reportScope = _scopeFactory.CreateScope())
                {
                    await reportScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.Stores.StoreMonthlyReports>().RunAsync(DateTime.UtcNow, ct);
                }

                if (_configuration.GetValue("Jobs:WeeklyDigest:Enabled", true))
                {
                    using var digestScope = _scopeFactory.CreateScope();
                    await digestScope.ServiceProvider.GetRequiredService<WeeklyDigest>().RunAsync(DateTime.UtcNow, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // arresto dell'applicazione
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manutenzione notturna fallita");
            }
        }

        public static TimeSpan DelayUntilNextRun(string runAtUtc, DateTime nowUtc)
        {
            if (!TimeSpan.TryParse(runAtUtc, System.Globalization.CultureInfo.InvariantCulture, out var timeOfDay) ||
                timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
            {
                timeOfDay = new TimeSpan(2, 30, 0);
            }
            var next = nowUtc.Date + timeOfDay;
            if (next <= nowUtc) next = next.AddDays(1);
            return next - nowUtc;
        }
    }
}
