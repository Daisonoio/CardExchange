using CardExchange.API.Services.Entitlements;
using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Notifications;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Billing
{
    public class PromotionException : Exception
    {
        public int Status { get; }
        public string Code { get; }
        public PromotionException(string message, string code = "PROMOTION", int status = 400) : base(message) { Code = code; Status = status; }
    }

    public record PromotionTarget(int OwnerId, int? StoreId, string Title, string? Text, decimal Latitude, decimal Longitude, DateTime EndsAt, int? GameId, string? City);

    public record PromotionEstimate(int InRadius, int Reachable, int PriceCredits, decimal PriceEur);

    /// <summary>
    /// Promozioni locali: evento in evidenza e "Sono qui" sponsorizzato. Si pagano con i crediti (del piano o acquistati)
    /// o con un acquisto Stripe. Consegna con limiti per persona (2 a settimana, mai due volte la stessa),
    /// mai ai minorenni né a chi ha disattivato le proposte sponsorizzate.
    /// </summary>
    public class PromotionService
    {
        /// <summary>Sotto questa soglia le stime non si mostrano (dati aggregati da almeno 3 persone).</summary>
        public const int MinShown = 3;

        private readonly ApplicationDbContext _db;
        private readonly INotificationDispatcher _dispatcher;
        private readonly IUserBlockService _blocks;
        private readonly IEntitlementService _entitlements;
        private readonly ILogger<PromotionService> _logger;

        public PromotionService(ApplicationDbContext db, INotificationDispatcher dispatcher, IUserBlockService blocks, ILogger<PromotionService> logger,
            IEntitlementService? entitlements = null)
        {
            _db = db;
            _dispatcher = dispatcher;
            _blocks = blocks;
            _logger = logger;
            _entitlements = entitlements.OrDefault(db);
        }

        public async Task<int> CreditBalanceAsync(int userId) =>
            await _db.PromotionCreditEntries.Where(e => e.UserId == userId).SumAsync(e => (int?)e.Delta) ?? 0;

        // ---------------------------------------------------------------- bersaglio

        public async Task<PromotionTarget> TargetAsync(int userId, PromotionType type, int targetId, DateTime nowUtc)
        {
            switch (type)
            {
                case PromotionType.FeaturedEvent:
                {
                    var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == targetId)
                        ?? throw new PromotionException("Evento non trovato", status: 404);
                    if (ev.OrganizerId != userId) throw new PromotionException("Puoi promuovere solo i tuoi eventi", status: 403);
                    if (ev.Status != EventStatus.Published || !ev.IsPublic) throw new PromotionException("L'evento deve essere pubblicato e pubblico");
                    if (ev.StartDate <= nowUtc) throw new PromotionException("L'evento è già iniziato");
                    if (ev.Latitude == null || ev.Longitude == null) throw new PromotionException("L'evento non ha una posizione sulla mappa");
                    return new(ev.OrganizerId, ev.StoreId, ev.Title, ev.Description, ev.Latitude.Value, ev.Longitude.Value, ev.StartDate, ev.GameId, ev.City);
                }
                case PromotionType.SponsoredBroadcast:
                {
                    var b = await _db.LocationBroadcasts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == targetId)
                        ?? throw new PromotionException("Avviso non trovato", status: 404);
                    if (b.AuthorId != userId) throw new PromotionException("Puoi promuovere solo i tuoi avvisi", status: 403);
                    if (b.EndedAt != null || b.EndsAt <= nowUtc) throw new PromotionException("L'avviso è già terminato");
                    return new(b.AuthorId, b.StoreId, b.PlaceName, b.Message, b.Latitude, b.Longitude, b.EndsAt, null, null);
                }
                default:
                    throw new PromotionException("Tipo di promozione non valido");
            }
        }

        private async Task EnsureAdultAsync(int userId)
        {
            var birth = await _db.Users.Where(u => u.Id == userId).Select(u => u.BirthDate).FirstOrDefaultAsync();
            if (birth is not { } bd || AgeRules.IsMinor(bd, DateOnly.FromDateTime(DateTime.UtcNow)))
                throw new PromotionException("Le promozioni sono riservate ai maggiorenni", "ADULTS_ONLY", 403);
        }

        // ---------------------------------------------------------------- pubblico

        /// <summary>
        /// Persone nel raggio e persone che riceverebbero la notifica: maggiorenni, non bloccate,
        /// che non hanno disattivato le proposte sponsorizzate e (per "Sono qui") che cercano una carta dell'autore.
        /// </summary>
        private async Task<(List<int> InRadius, List<int> Eligible)> AudienceAsync(PromotionType type, PromotionTarget target, int radiusKm, int targetId, int? gameId)
        {
            var lat = (double)target.Latitude;
            var lon = (double)target.Longitude;
            var dLat = (decimal)(radiusKm / 111.0);
            var dLon = (decimal)(radiusKm / Math.Max(1, 111.0 * Math.Cos(lat * Math.PI / 180)));
            var blocked = await _blocks.GetBlockedUserIdsAsync(target.OwnerId);
            var rows = await _db.UserLocations.AsNoTracking()
                .Where(l => l.UserId != target.OwnerId && l.Latitude != null && l.Longitude != null
                            && l.Latitude >= target.Latitude - dLat && l.Latitude <= target.Latitude + dLat
                            && l.Longitude >= target.Longitude - dLon && l.Longitude <= target.Longitude + dLon
                            && l.User.IsActive)
                .Select(l => new { l.UserId, Lat = l.Latitude!.Value, Lon = l.Longitude!.Value, l.User.BirthDate })
                .ToListAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var inRadius = rows
                .Where(r => !blocked.Contains(r.UserId) && Geo.DistanceKm(lat, lon, (double)r.Lat, (double)r.Lon) <= radiusKm)
                .ToList();
            // Minorenni ed età non indicata: nessuna notifica sponsorizzata
            var adults = inRadius.Where(r => r.BirthDate is { } bd && !AgeRules.IsMinor(bd, today)).Select(r => r.UserId).ToList();

            var optedOut = (await _db.NotificationPreferences.AsNoTracking()
                .Where(p => adults.Contains(p.UserId) && p.Type == NotificationType.Sponsored && !p.IsEnabled)
                .Select(p => p.UserId).ToListAsync()).ToHashSet();
            var eligible = adults.Where(id => !optedOut.Contains(id)).ToList();

            if (type == PromotionType.FeaturedEvent && gameId.HasValue)
            {
                // Solo chi ha quel gioco in collezione o in wishlist
                var players = (await _db.Cards.AsNoTracking().Where(c => eligible.Contains(c.UserId) && c.CardInfo.CardSet.GameId == gameId.Value).Select(c => c.UserId)
                    .Union(_db.WishlistItems.AsNoTracking().Where(w => eligible.Contains(w.UserId) && w.CardInfo.CardSet.GameId == gameId.Value).Select(w => w.UserId))
                    .ToListAsync()).ToHashSet();
                eligible = eligible.Where(players.Contains).ToList();
            }
            else if (type == PromotionType.SponsoredBroadcast)
            {
                // Chi segue già l'autore riceve l'avviso normale; gli altri solo se cercano una sua carta disponibile
                var followers = (await _db.Follows.AsNoTracking()
                    .Where(f => f.FollowedUserId == target.OwnerId || (target.StoreId != null && f.FollowedStoreId == target.StoreId))
                    .Select(f => f.FollowerId).ToListAsync()).ToHashSet();
                var available = _db.Cards.Where(c => c.UserId == target.OwnerId && c.IsAvailableForTrade && c.Quantity > c.ReservedQuantity).Select(c => c.CardInfoId);
                var wanting = (await _db.WishlistItems.AsNoTracking()
                    .Where(w => eligible.Contains(w.UserId) && available.Contains(w.CardInfoId))
                    .Select(w => w.UserId).Distinct().ToListAsync()).ToHashSet();
                eligible = eligible.Where(id => !followers.Contains(id) && wanting.Contains(id)).ToList();
            }
            return (inRadius.Select(r => r.UserId).ToList(), eligible);
        }

        /// <summary>Stima per l'anteprima: sotto le 3 persone si mostra 0 ("meno di 3").</summary>
        public async Task<PromotionEstimate> EstimateAsync(int userId, PromotionType type, int targetId, int radiusKm, DateTime nowUtc)
        {
            radiusKm = PromotionRules.ClampRadius(radiusKm);
            var target = await TargetAsync(userId, type, targetId, nowUtc);
            var (inRadius, eligible) = await AudienceAsync(type, target, radiusKm, targetId, target.GameId);
            static int Safe(int n) => n >= MinShown ? n : 0;
            return new(Safe(inRadius.Count), Safe(eligible.Count), PromotionRules.PriceCredits(type, radiusKm), PromotionRules.PriceEur(type, radiusKm));
        }

        // ---------------------------------------------------------------- ciclo di vita

        public async Task<Promotion> CreateAsync(int userId, PromotionType type, int targetId, int radiusKm, int? days, DateTime nowUtc)
        {
            await EnsureAdultAsync(userId);
            radiusKm = PromotionRules.ClampRadius(radiusKm);
            var target = await TargetAsync(userId, type, targetId, nowUtc);
            if (await _db.Promotions.AnyAsync(p => p.Type == type && p.TargetId == targetId && p.Status == PromotionStatus.Active && p.EndsAt > nowUtc))
                throw new PromotionException("Questa promozione è già attiva o in attesa di pagamento", "DUPLICATE", 409);

            // Revisione automatica: niente link, contatti o pagamenti fuori dall'app
            var reason = PromotionRules.Review(target.Title, target.Text);
            if (reason != null) throw new PromotionException(reason, "REJECTED");

            var endsAt = type == PromotionType.FeaturedEvent
                ? new[] { target.EndsAt, nowUtc.AddDays(Math.Clamp(days ?? PromotionRules.MaxFeaturedDays, 1, PromotionRules.MaxFeaturedDays)) }.Min()
                : target.EndsAt;
            // Pagamento non riuscito o abbandonato: si riprende la stessa bozza con le nuove scelte
            var pending = await _db.Promotions.FirstOrDefaultAsync(p => p.OwnerUserId == userId && p.Type == type && p.TargetId == targetId
                && p.Status == PromotionStatus.PendingPayment);
            if (pending != null)
            {
                pending.RadiusKm = radiusKm;
                pending.PriceCredits = PromotionRules.PriceCredits(type, radiusKm);
                pending.EndsAt = endsAt;
                pending.CreatedAt = nowUtc;
                await _db.SaveChangesAsync();
                return pending;
            }
            var promo = new Promotion
            {
                OwnerUserId = userId, StoreId = target.StoreId, Type = type, TargetId = targetId, RadiusKm = radiusKm, GameId = target.GameId,
                Latitude = target.Latitude, Longitude = target.Longitude, StartsAt = nowUtc, EndsAt = endsAt,
                Status = PromotionStatus.PendingPayment, PriceCredits = PromotionRules.PriceCredits(type, radiusKm), CreatedAt = nowUtc
            };
            _db.Promotions.Add(promo);
            await _db.SaveChangesAsync();
            return promo;
        }

        /// <summary>Pagamento con i crediti: la promozione passa ad attiva una sola volta e i crediti si scalano nella stessa transazione.</summary>
        public async Task<Promotion> PayWithCreditsAsync(int userId, int promotionId, DateTime nowUtc)
        {
            var promo = await _db.InTransactionAsync(async () =>
            {
                var p = await _db.Promotions.FirstOrDefaultAsync(x => x.Id == promotionId && x.OwnerUserId == userId)
                    ?? throw new PromotionException("Promozione non trovata", status: 404);
                if (p.Status != PromotionStatus.PendingPayment) throw new PromotionException("Questa promozione non è in attesa di pagamento");
                if (p.EndsAt <= nowUtc) throw new PromotionException("Il periodo della promozione è già finito");
                var balance = await CreditBalanceAsync(userId);
                if (balance < p.PriceCredits)
                    throw new PromotionException($"Crediti insufficienti: ne servono {p.PriceCredits}, ne hai {balance}", "NOT_ENOUGH_CREDITS", 402);
                var updated = await _db.Promotions.Where(x => x.Id == p.Id && x.Status == PromotionStatus.PendingPayment)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, PromotionStatus.Active).SetProperty(x => x.PaidWith, PromotionPayment.Credits)
                        .SetProperty(x => x.StartsAt, nowUtc));
                if (updated == 0) throw new PromotionException("Questa promozione non è in attesa di pagamento");
                _db.PromotionCreditEntries.Add(new PromotionCreditEntry { UserId = userId, Delta = -p.PriceCredits, Reason = $"Promozione #{p.Id}", CreatedAt = nowUtc });
                await _db.SaveChangesAsync();
                return p;
            });
            await _db.Entry(promo).ReloadAsync();
            await DeliverAsync(promo, nowUtc);
            return promo;
        }

        /// <summary>Annullata prima della consegna: i crediti tornano indietro.</summary>
        public async Task CancelAsync(int userId, int promotionId, DateTime nowUtc)
        {
            var p = await _db.Promotions.FirstOrDefaultAsync(x => x.Id == promotionId && x.OwnerUserId == userId)
                ?? throw new PromotionException("Promozione non trovata", status: 404);
            if (p.Status is PromotionStatus.Cancelled or PromotionStatus.Completed or PromotionStatus.Rejected)
                throw new PromotionException("Questa promozione è già chiusa");
            var refund = p.Status == PromotionStatus.Active && p.DeliveredAt == null && p.PaidWith != PromotionPayment.None;
            p.Status = PromotionStatus.Cancelled;
            p.EndsAt = new[] { p.EndsAt, nowUtc }.Min();
            if (refund)
                _db.PromotionCreditEntries.Add(new PromotionCreditEntry { UserId = userId, Delta = p.PriceCredits, Reason = $"Promozione #{p.Id} annullata", CreatedAt = nowUtc });
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Consegna delle notifiche: una volta per promozione. Ogni persona riceve al massimo 2 notifiche sponsorizzate
        /// in 7 giorni e mai due volte la stessa promozione.
        /// </summary>
        public async Task<int> DeliverAsync(Promotion promo, DateTime nowUtc)
        {
            if (promo.Status != PromotionStatus.Active || promo.DeliveredAt != null || promo.StartsAt > nowUtc) return 0;
            // Prenota la consegna: due esecuzioni contemporanee non mandano due volte
            var claimed = await _db.Promotions.Where(p => p.Id == promo.Id && p.DeliveredAt == null)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.DeliveredAt, nowUtc));
            if (claimed == 0) return 0;
            promo.DeliveredAt = nowUtc;

            PromotionTarget target;
            try { target = await TargetAsync(promo.OwnerUserId, promo.Type, promo.TargetId, nowUtc); }
            catch (PromotionException ex)
            {
                _logger.LogInformation("Promozione {Id} non consegnata: {Reason}", promo.Id, ex.Message);
                return 0;
            }
            var (_, eligible) = await AudienceAsync(promo.Type, target, promo.RadiusKm, promo.TargetId, promo.GameId);
            if (eligible.Count == 0) return 0;

            var since = nowUtc - PromotionRules.CapWindow;
            var recent = await _db.PromotionDeliveries.AsNoTracking()
                .Where(d => eligible.Contains(d.UserId) && d.SentAt >= since)
                .GroupBy(d => d.UserId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            var full = recent.Where(r => r.Count >= PromotionRules.MaxSponsoredPerWeek).Select(r => r.Key).ToHashSet();
            var already = (await _db.PromotionDeliveries.AsNoTracking().Where(d => d.PromotionId == promo.Id).Select(d => d.UserId).ToListAsync()).ToHashSet();
            var recipients = eligible.Where(id => !full.Contains(id) && !already.Contains(id)).ToList();
            if (recipients.Count == 0) return 0;

            var owner = await _db.Users.AsNoTracking().Where(u => u.Id == promo.OwnerUserId).Select(u => u.Username).FirstAsync();
            var storeName = promo.StoreId == null ? null : await _db.Stores.AsNoTracking().Where(s => s.Id == promo.StoreId).Select(s => s.Name).FirstOrDefaultAsync();
            var from = storeName ?? owner;
            var payloads = recipients.Select(id => promo.Type == PromotionType.FeaturedEvent
                ? new NotificationPayload
                {
                    UserId = id, Type = NotificationType.Sponsored, ReferenceId = promo.TargetId, ReferenceType = "Event",
                    Title = $"Evento vicino: {target.Title}",
                    Body = $"Sponsorizzato da {from}. {target.City}, {TimeZones.FormatWhen(target.EndsAt, null)}."
                }
                : new NotificationPayload
                {
                    UserId = id, Type = NotificationType.Sponsored, ReferenceId = promo.TargetId, ReferenceType = "Broadcast",
                    Title = $"{from} è qui vicino: {target.Title}",
                    Body = $"Sponsorizzato. Ha disponibile una carta che cerchi, fino alle {TimeZones.FormatTime(target.EndsAt, null)}. Prezzo e scambio si concordano di persona."
                }).ToList();

            _db.PromotionDeliveries.AddRange(recipients.Select(id => new PromotionDelivery { PromotionId = promo.Id, UserId = id, SentAt = nowUtc }));
            await _db.SaveChangesAsync();
            var delivered = await _dispatcher.DeliverManyAsync(payloads);
            await _db.Promotions.Where(p => p.Id == promo.Id).ExecuteUpdateAsync(u => u.SetProperty(p => p.Reach, p => p.Reach + recipients.Count));
            promo.Reach += recipients.Count;
            _logger.LogInformation("Promozione {Id}: {Count} notifiche consegnate", promo.Id, delivered);
            return recipients.Count;
        }

        /// <summary>Ogni 15 minuti: consegna le promozioni pagate con un acquisto e chiude quelle finite.</summary>
        public async Task RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var due = await _db.Promotions.Where(p => p.Status == PromotionStatus.Active && p.DeliveredAt == null && p.StartsAt <= nowUtc && p.EndsAt > nowUtc)
                .Take(50).ToListAsync(ct);
            foreach (var p in due) await DeliverAsync(p, nowUtc);
            await _db.Promotions.Where(p => p.Status == PromotionStatus.Active && p.EndsAt <= nowUtc)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PromotionStatus.Completed), ct);
            // Pagamenti mai arrivati: dopo 2 giorni la bozza si annulla
            await _db.Promotions.Where(p => p.Status == PromotionStatus.PendingPayment && (p.CreatedAt < nowUtc.AddDays(-2) || p.EndsAt <= nowUtc))
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PromotionStatus.Cancelled), ct);
        }

        /// <summary>
        /// Crediti del piano (Banco 2, Negozio Pro 4 al mese): accreditati una volta per mese di calendario a chi ha il piano attivo.
        /// </summary>
        public async Task<int> GrantMonthlyCreditsAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var local = TimeZones.ToLocal(nowUtc, null);
            var reason = $"Crediti del piano {local:yyyy-MM}";
            var paying = await _db.UserSubscriptions.AsNoTracking()
                .Where(s => s.Status == SubscriptionStatus.Active && s.StartDate <= nowUtc && (s.EndDate == null || s.EndDate > nowUtc))
                .Select(s => s.UserId).Distinct().ToListAsync(ct);
            if (paying.Count == 0) return 0;
            var granted = (await _db.PromotionCreditEntries.AsNoTracking().Where(e => paying.Contains(e.UserId) && e.Reason == reason)
                .Select(e => e.UserId).ToListAsync(ct)).ToHashSet();
            var rights = await _entitlements.GetManyAsync(paying.Where(id => !granted.Contains(id)));
            var count = 0;
            foreach (var (userId, e) in rights)
            {
                var credits = e.Limit(PlanLimit.MonthlyPromotionCredits);
                if (credits <= 0) continue;
                _db.PromotionCreditEntries.Add(new PromotionCreditEntry { UserId = userId, Delta = credits, Reason = reason, CreatedAt = nowUtc });
                count++;
            }
            await _db.SaveChangesAsync(ct);
            return count;
        }

        // ---------------------------------------------------------------- visibilità

        /// <summary>Eventi in evidenza per chi guarda (nel raggio della promozione). I minorenni li vedono solo qui, nella lista.</summary>
        public async Task<List<(Promotion Promo, Event Event)>> FeaturedEventsAsync(int viewerId, decimal? latitude, decimal? longitude, DateTime nowUtc)
        {
            if (latitude == null || longitude == null)
            {
                var loc = await _db.UserLocations.AsNoTracking().FirstOrDefaultAsync(l => l.UserId == viewerId);
                latitude = loc?.Latitude; longitude = loc?.Longitude;
            }
            if (latitude == null || longitude == null) return new();
            var active = await _db.Promotions.AsNoTracking()
                .Where(p => p.Type == PromotionType.FeaturedEvent && p.Status == PromotionStatus.Active && p.StartsAt <= nowUtc && p.EndsAt > nowUtc)
                .ToListAsync();
            var near = active.Where(p => Geo.DistanceKm((double)latitude.Value, (double)longitude.Value, (double)p.Latitude, (double)p.Longitude) <= p.RadiusKm).ToList();
            if (near.Count == 0) return new();
            var blocked = await _blocks.GetBlockedUserIdsAsync(viewerId);
            var ids = near.Select(p => p.TargetId).ToList();
            var events = await _db.Events.AsNoTracking().Include(e => e.Organizer).Include(e => e.Store)
                .Where(e => ids.Contains(e.Id) && e.Status == EventStatus.Published && e.StartDate > nowUtc)
                .ToDictionaryAsync(e => e.Id);
            var list = near.Where(p => events.ContainsKey(p.TargetId) && !blocked.Contains(p.OwnerUserId))
                .OrderBy(p => events[p.TargetId].StartDate).Take(3)
                .Select(p => (p, events[p.TargetId])).ToList();
            var shown = list.Select(x => x.p.Id).ToList();
            if (shown.Count > 0)
                await _db.Promotions.Where(p => shown.Contains(p.Id)).ExecuteUpdateAsync(u => u.SetProperty(p => p.Impressions, p => p.Impressions + 1));
            return list;
        }

        public Task ClickAsync(int promotionId) =>
            _db.Promotions.Where(p => p.Id == promotionId).ExecuteUpdateAsync(u => u.SetProperty(p => p.Clicks, p => p.Clicks + 1));

        /// <summary>Segnalazione: una per persona; con troppe segnalazioni la promozione si ferma.</summary>
        public async Task<bool> ReportAsync(int userId, int promotionId, string? reason, DateTime nowUtc)
        {
            var p = await _db.Promotions.FirstOrDefaultAsync(x => x.Id == promotionId) ?? throw new PromotionException("Promozione non trovata", status: 404);
            if (await _db.PromotionReports.AnyAsync(r => r.PromotionId == promotionId && r.UserId == userId)) return false;
            _db.PromotionReports.Add(new PromotionReport { PromotionId = promotionId, UserId = userId, Reason = reason?.Trim() is { Length: > 0 } r ? r[..Math.Min(200, r.Length)] : null, CreatedAt = nowUtc });
            p.Reports++;
            if (p.Status == PromotionStatus.Active && PromotionRules.ShouldPause(p.Reports, p.Reach))
            {
                p.Status = PromotionStatus.Rejected;
                p.RejectionReason = "Fermata per troppe segnalazioni";
                _logger.LogWarning("Promozione {Id} fermata: {Reports} segnalazioni su {Reach} consegne", p.Id, p.Reports, p.Reach);
            }
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
