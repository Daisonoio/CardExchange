using CardExchange.API.Services.Entitlements;
using System.Globalization;
using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Notifications;
using CardExchange.Core.Constants;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Stores
{
    public class BuyListException : Exception
    {
        public BuyListException(string message) : base(message) { }
    }

    public record BuyOrderInput(int CardInfoId, CardCondition MinCondition, CardFinish? Finish, string? Language,
        decimal? PriceCashEur, decimal? PriceCreditEur, int QuantityWanted, DateTime? ExpiresAt, bool IsActive = true);

    /// <summary>Una richiesta di un negozio vicino che corrisponde a carte della mia collezione.</summary>
    public record BuyOrderForMe(StoreBuyOrder Order, Store Store, int DistanceKm, List<Card> MyCards, StoreBuyOffer? MyOffer);

    /// <summary>
    /// Lista acquisti del negozio: il negozio pubblica cosa compra e a che prezzo, chi ha la carta la propone
    /// e la porta in negozio. Nessun pagamento nell'app: contanti o credito si regolano alla consegna.
    /// Avvisi e proposte solo ad adulti (niente proposte basate sulla collezione ai minorenni).
    /// </summary>
    public class BuyListService
    {
        public const int DefaultRadiusKm = 30;
        public static readonly TimeSpan DropOffWindow = TimeSpan.FromDays(7);
        public static readonly TimeSpan ProposalLifetime = TimeSpan.FromDays(14);
        public static readonly TimeSpan AlertInterval = TimeSpan.FromDays(7);
        public static readonly BuyOfferStatus[] OpenStatuses = { BuyOfferStatus.Proposed, BuyOfferStatus.Accepted };
        private static readonly CultureInfo Italian = new("it-IT");

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly IUserBlockService _blocks;
        private readonly ILogger<BuyListService> _logger;

        private readonly IEntitlementService _entitlements;

        public BuyListService(ApplicationDbContext db, INotificationService notifications, IUserBlockService blocks, ILogger<BuyListService> logger,
            IEntitlementService? entitlements = null)
        {
            _entitlements = entitlements.OrDefault(db);
            _db = db;
            _notifications = notifications;
            _blocks = blocks;
            _logger = logger;
        }

        public static bool Matches(Card card, StoreBuyOrder order) =>
            card.CardInfoId == order.CardInfoId
            && card.Condition <= order.MinCondition
            && (order.Finish == null || card.Finish == order.Finish)
            && (order.Language == null || card.Language == order.Language);

        public static int Remaining(StoreBuyOrder o) => Math.Max(0, o.QuantityWanted - o.QuantityFilled - o.QuantityPending);

        public static bool IsAdult(User u, DateTime nowUtc) => u.BirthDate is { } bd && !AgeRules.IsMinor(bd, DateOnly.FromDateTime(nowUtc));

        public static string PriceText(StoreBuyOrder o)
        {
            string Eur(decimal v) => v.ToString("0.##", Italian) + " €";
            return (o.PriceCreditEur, o.PriceCashEur) switch
            {
                ({ } cr, { } ca) => $"{Eur(cr)} in credito o {Eur(ca)} in contanti",
                ({ } cr, null) => $"{Eur(cr)} in credito",
                (null, { } ca) => $"{Eur(ca)} in contanti",
                _ => "prezzo da concordare"
            };
        }

        /// <summary>Il negozio può usare la lista acquisti: verificato e con il piano Negozio Pro.</summary>
        public async Task<bool> StoreHasPlanAsync(Store store, DateTime nowUtc) =>
            store.Status == StoreStatus.Verified && await _entitlements.HasAsync(store.OwnerId, Capability.Buylist);

        private static void Validate(BuyOrderInput i)
        {
            if (i.QuantityWanted is < 1 or > 100) throw new BuyListException("Quantità deve essere tra 1 e 100");
            if (i.PriceCashEur is < 0 || i.PriceCreditEur is < 0) throw new BuyListException("Importo non valido");
            if (i.PriceCashEur == null && i.PriceCreditEur == null) throw new BuyListException("Indica un prezzo in contanti o in credito");
            if (!Enum.IsDefined(i.MinCondition)) throw new BuyListException("Condizione non valida");
        }

        public async Task<StoreBuyOrder> CreateOrderAsync(Store store, BuyOrderInput input, DateTime nowUtc)
        {
            Validate(input);
            if (!await _db.CardInfos.AnyAsync(ci => ci.Id == input.CardInfoId)) throw new BuyListException("Carta non trovata");
            if (await _db.StoreBuyOrders.AnyAsync(o => o.StoreId == store.Id && o.CardInfoId == input.CardInfoId && o.IsActive
                    && o.MinCondition == input.MinCondition && o.Finish == input.Finish && o.Language == input.Language))
                throw new BuyListException("Questa carta è già nella tua lista acquisti con le stesse condizioni");
            var order = new StoreBuyOrder { StoreId = store.Id };
            Apply(order, input);
            _db.StoreBuyOrders.Add(order);
            await _db.SaveChangesAsync();
            return order;
        }

        public async Task<StoreBuyOrder> UpdateOrderAsync(Store store, int orderId, BuyOrderInput input)
        {
            Validate(input);
            var order = await _db.StoreBuyOrders.FirstOrDefaultAsync(o => o.Id == orderId && o.StoreId == store.Id)
                        ?? throw new BuyListException("Richiesta non trovata");
            if (input.QuantityWanted < order.QuantityFilled + order.QuantityPending)
                throw new BuyListException("Hai già accettato o ricevuto più copie di così");
            Apply(order, input with { CardInfoId = order.CardInfoId });
            await _db.SaveChangesAsync();
            return order;
        }

        private static void Apply(StoreBuyOrder o, BuyOrderInput i)
        {
            o.CardInfoId = i.CardInfoId;
            o.MinCondition = i.MinCondition;
            o.Finish = i.Finish;
            o.Language = string.IsNullOrWhiteSpace(i.Language) ? null : CardLanguages.Normalize(i.Language) ?? i.Language.Trim().ToLowerInvariant();
            o.PriceCashEur = i.PriceCashEur;
            o.PriceCreditEur = i.PriceCreditEur;
            o.QuantityWanted = i.QuantityWanted;
            o.ExpiresAt = i.ExpiresAt;
            o.IsActive = i.IsActive;
        }

        /// <summary>Richieste attive di negozi vicini che corrispondono alle carte della mia collezione.</summary>
        public async Task<List<BuyOrderForMe>> ForMeAsync(int userId, DateTime nowUtc)
        {
            var me = await _db.Users.AsNoTracking().Include(u => u.Location).FirstOrDefaultAsync(u => u.Id == userId);
            if (me?.Location?.Latitude == null || me.Location.Longitude == null || !IsAdult(me, nowUtc)) return new();
            var radius = me.Location.MaxDistanceKm > 0 ? me.Location.MaxDistanceKm : DefaultRadiusKm;
            var myCards = await _db.Cards.AsNoTracking().Where(c => c.UserId == userId).ToListAsync();
            if (myCards.Count == 0) return new();
            var cardInfoIds = myCards.Select(c => c.CardInfoId).Distinct().ToList();
            var orders = await _db.StoreBuyOrders.AsNoTracking()
                .Include(o => o.Store).Include(o => o.CardInfo).ThenInclude(ci => ci.CardSet)
                .Where(o => o.IsActive && cardInfoIds.Contains(o.CardInfoId) && o.Store.Status == StoreStatus.Verified
                            && o.Store.OwnerId != userId && (o.ExpiresAt == null || o.ExpiresAt > nowUtc)
                            && o.QuantityWanted - o.QuantityFilled - o.QuantityPending > 0)
                .ToListAsync();
            var blocked = await _blocks.GetBlockedUserIdsAsync(userId);
            var orderIds = orders.Select(o => o.Id).ToList();
            var myOffers = await _db.StoreBuyOffers.AsNoTracking()
                .Where(o => o.UserId == userId && orderIds.Contains(o.BuyOrderId))
                .OrderByDescending(o => o.CreatedAt).ToListAsync();

            var result = new List<BuyOrderForMe>();
            foreach (var o in orders.Where(o => !blocked.Contains(o.Store.OwnerId)))
            {
                var km = Geo.DistanceKm((double)me.Location.Latitude, (double)me.Location.Longitude, (double)o.Store.Latitude, (double)o.Store.Longitude);
                if (km > radius) continue;
                var matching = myCards.Where(c => Matches(c, o)).ToList();
                if (matching.Count == 0) continue;
                result.Add(new BuyOrderForMe(o, o.Store, BroadcastRules.DisplayDistanceKm(km)!.Value, matching, myOffers.FirstOrDefault(x => x.BuyOrderId == o.Id)));
            }
            return result.OrderBy(r => r.DistanceKm).ThenByDescending(r => r.Order.PriceCreditEur ?? r.Order.PriceCashEur ?? 0).ToList();
        }

        public async Task<StoreBuyOffer> ProposeAsync(int userId, int orderId, int cardId, int quantity, DateTime nowUtc)
        {
            if (quantity < 1) throw new BuyListException("La quantità deve essere almeno 1");
            var me = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
            if (!IsAdult(me, nowUtc)) throw new BuyListException("La lista acquisti dei negozi è riservata ai maggiorenni");
            var order = await _db.StoreBuyOrders.Include(o => o.Store).Include(o => o.CardInfo).FirstOrDefaultAsync(o => o.Id == orderId)
                        ?? throw new BuyListException("Richiesta non trovata");
            if (!order.IsActive || (order.ExpiresAt != null && order.ExpiresAt <= nowUtc) || Remaining(order) == 0)
                throw new BuyListException("Il negozio non cerca più questa carta");
            if (order.Store.OwnerId == userId) throw new BuyListException("Non puoi proporre carte al tuo negozio");
            if (await _blocks.IsBlockedAsync(userId, order.Store.OwnerId)) throw new BuyListException("Non puoi proporre carte a questo negozio");
            var card = await _db.Cards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId)
                       ?? throw new BuyListException("Carta non trovata");
            if (!Matches(card, order)) throw new BuyListException("Questa copia non corrisponde alla richiesta (condizione, lingua o finitura)");
            if (quantity > card.Quantity - card.ReservedQuantity) throw new BuyListException("Non hai abbastanza copie di questa carta");
            if (quantity > Remaining(order)) throw new BuyListException($"Il negozio cerca ancora {Remaining(order)} copie");
            if (await _db.StoreBuyOffers.AnyAsync(o => o.BuyOrderId == orderId && o.UserId == userId && OpenStatuses.Contains(o.Status)))
                throw new BuyListException("Hai già una proposta aperta per questa richiesta");

            var offer = new StoreBuyOffer { BuyOrderId = orderId, UserId = userId, CardId = cardId, Quantity = quantity };
            _db.StoreBuyOffers.Add(offer);
            await _db.SaveChangesAsync();
            await NotifyAsync(order.Store.OwnerId, "Nuova proposta per la lista acquisti",
                $"@{me.Username} propone {order.CardInfo.Name} (x{quantity}) per {PriceText(order)}.", order.StoreId, "StoreBuyList");
            return offer;
        }

        private async Task<StoreBuyOffer> LoadOfferAsync(int offerId) =>
            await _db.StoreBuyOffers.Include(o => o.BuyOrder).ThenInclude(b => b.Store)
                .Include(o => o.BuyOrder).ThenInclude(b => b.CardInfo).Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == offerId) ?? throw new BuyListException("Proposta non trovata");

        private static void RequireOwner(StoreBuyOffer o, int ownerId)
        {
            if (o.BuyOrder.Store.OwnerId != ownerId) throw new UnauthorizedAccessException();
        }

        public async Task<StoreBuyOffer> AcceptAsync(int offerId, int ownerId, string? note, DateTime nowUtc)
        {
            var offer = await LoadOfferAsync(offerId);
            RequireOwner(offer, ownerId);
            if (offer.Status != BuyOfferStatus.Proposed) throw new BuyListException("Questa proposta non è più in attesa");
            await _db.InTransactionAsync(async () =>
            {
                // Mai più copie accettate di quelle richieste, anche con due accettazioni contemporanee
                var ok = await _db.StoreBuyOrders
                    .Where(o => o.Id == offer.BuyOrderId && o.QuantityWanted - o.QuantityFilled - o.QuantityPending >= offer.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.QuantityPending, o => o.QuantityPending + offer.Quantity));
                if (ok == 0) throw new BuyListException("Hai già accettato tutte le copie richieste");
                offer.Status = BuyOfferStatus.Accepted;
                offer.RespondedAt = nowUtc;
                offer.DropOffBy = nowUtc + DropOffWindow;
                offer.StoreNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(300, note.Trim().Length)];
                await _db.SaveChangesAsync();
            });
            var s = offer.BuyOrder.Store;
            await NotifyAsync(offer.UserId, "Il negozio compra la tua carta",
                $"{s.Name} ha accettato {offer.BuyOrder.CardInfo.Name} (x{offer.Quantity}) per {PriceText(offer.BuyOrder)}. Portala entro {TimeZones.FormatWhen(offer.DropOffBy!.Value, offer.User.TimeZone)} in {s.Address}, {s.City}: si paga in negozio.",
                offer.Id, "BuyOffer");
            return offer;
        }

        public async Task<StoreBuyOffer> RejectAsync(int offerId, int ownerId, string? note, DateTime nowUtc)
        {
            var offer = await LoadOfferAsync(offerId);
            RequireOwner(offer, ownerId);
            if (offer.Status != BuyOfferStatus.Proposed) throw new BuyListException("Questa proposta non è più in attesa");
            offer.Status = BuyOfferStatus.Rejected;
            offer.RespondedAt = offer.ClosedAt = nowUtc;
            offer.StoreNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(300, note.Trim().Length)];
            await _db.SaveChangesAsync();
            await NotifyAsync(offer.UserId, "Proposta non accettata", $"{offer.BuyOrder.Store.Name} non ha accettato la proposta per {offer.BuyOrder.CardInfo.Name}.", offer.Id, "BuyOffer");
            return offer;
        }

        public async Task<StoreBuyOffer> WithdrawAsync(int offerId, int userId, DateTime nowUtc)
        {
            var offer = await LoadOfferAsync(offerId);
            if (offer.UserId != userId) throw new UnauthorizedAccessException();
            if (!OpenStatuses.Contains(offer.Status)) throw new BuyListException("Questa proposta non è più aperta");
            var wasAccepted = offer.Status == BuyOfferStatus.Accepted;
            await CloseAsync(offer, BuyOfferStatus.Withdrawn, nowUtc, releasePending: wasAccepted);
            if (wasAccepted)
                await NotifyAsync(offer.BuyOrder.Store.OwnerId, "Proposta ritirata", $"@{offer.User.Username} ha ritirato la proposta per {offer.BuyOrder.CardInfo.Name}.", offer.BuyOrder.StoreId, "StoreBuyList");
            return offer;
        }

        /// <summary>La carta è stata consegnata in negozio: esce dalla collezione dell'utente e la richiesta avanza.</summary>
        public async Task<StoreBuyOffer> CompleteAsync(int offerId, int ownerId, DateTime nowUtc)
        {
            var offer = await LoadOfferAsync(offerId);
            RequireOwner(offer, ownerId);
            if (offer.Status != BuyOfferStatus.Accepted) throw new BuyListException("Si può completare solo una proposta accettata");
            var q = offer.Quantity;
            await _db.InTransactionAsync(async () =>
            {
                await _db.StoreBuyOrders.Where(o => o.Id == offer.BuyOrderId).ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.QuantityPending, o => o.QuantityPending >= q ? o.QuantityPending - q : 0)
                    .SetProperty(o => o.QuantityFilled, o => o.QuantityFilled + q));
                await _db.StoreBuyOrders.Where(o => o.Id == offer.BuyOrderId && o.QuantityFilled >= o.QuantityWanted)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsActive, false));
                // La copia venduta esce dalla collezione (le copie prenotate in negozio restano intatte)
                await _db.Cards.Where(c => c.Id == offer.CardId).ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Quantity, c => c.Quantity >= q ? c.Quantity - q : 0));
                await _db.Cards.Where(c => c.Id == offer.CardId && c.Quantity <= 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true).SetProperty(c => c.IsAvailableForTrade, false));
                offer.Status = BuyOfferStatus.Completed;
                offer.ClosedAt = nowUtc;
                await _db.SaveChangesAsync();
            });
            await NotifyAsync(offer.UserId, "Vendita completata",
                $"Hai venduto {offer.BuyOrder.CardInfo.Name} (x{q}) a {offer.BuyOrder.Store.Name}: l'abbiamo tolta dalla tua collezione.", offer.Id, "BuyOffer");

            // Richiesta completata: le altre proposte in attesa si chiudono
            var order = await _db.StoreBuyOrders.AsNoTracking().FirstAsync(o => o.Id == offer.BuyOrderId);
            if (!order.IsActive)
            {
                var others = await _db.StoreBuyOffers.Include(o => o.BuyOrder).ThenInclude(b => b.CardInfo).Include(o => o.BuyOrder).ThenInclude(b => b.Store)
                    .Where(o => o.BuyOrderId == order.Id && o.Status == BuyOfferStatus.Proposed).ToListAsync();
                foreach (var o in others)
                {
                    o.Status = BuyOfferStatus.Rejected;
                    o.ClosedAt = nowUtc;
                    await NotifyAsync(o.UserId, "Proposta non accettata", $"{o.BuyOrder.Store.Name} ha già trovato tutte le copie di {o.BuyOrder.CardInfo.Name} che cercava.", o.Id, "BuyOffer");
                }
                if (others.Count > 0) await _db.SaveChangesAsync();
            }
            return offer;
        }

        private async Task CloseAsync(StoreBuyOffer offer, BuyOfferStatus status, DateTime nowUtc, bool releasePending)
        {
            var q = offer.Quantity;
            await _db.InTransactionAsync(async () =>
            {
                if (releasePending)
                    await _db.StoreBuyOrders.Where(o => o.Id == offer.BuyOrderId)
                        .ExecuteUpdateAsync(s => s.SetProperty(o => o.QuantityPending, o => o.QuantityPending >= q ? o.QuantityPending - q : 0));
                offer.Status = status;
                offer.ClosedAt = nowUtc;
                await _db.SaveChangesAsync();
            });
        }

        /// <summary>Ogni 15 minuti: promemoria il giorno prima della consegna, consegne scadute, proposte vecchie, richieste scadute.</summary>
        public async Task<(int Reminded, int Expired)> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var remind = await _db.StoreBuyOffers.Include(o => o.BuyOrder).ThenInclude(b => b.Store).Include(o => o.BuyOrder).ThenInclude(b => b.CardInfo).Include(o => o.User)
                .Where(o => o.Status == BuyOfferStatus.Accepted && o.ReminderSentAt == null && o.DropOffBy > nowUtc && o.DropOffBy <= nowUtc.AddHours(24))
                .ToListAsync(ct);
            foreach (var o in remind)
            {
                o.ReminderSentAt = nowUtc;
                var s = o.BuyOrder.Store;
                await NotifyAsync(o.UserId, "Domani scade la consegna",
                    $"Porta {o.BuyOrder.CardInfo.Name} (x{o.Quantity}) da {s.Name} entro {TimeZones.FormatWhen(o.DropOffBy!.Value, o.User.TimeZone)}.", o.Id, "BuyOffer");
                await NotifyAsync(s.OwnerId, "Consegna attesa domani",
                    $"@{o.User.Username} deve portarti {o.BuyOrder.CardInfo.Name} (x{o.Quantity}) entro domani.", s.Id, "StoreBuyList");
            }
            if (remind.Count > 0) await _db.SaveChangesAsync(ct);

            var expired = await _db.StoreBuyOffers.Include(o => o.BuyOrder).ThenInclude(b => b.Store).Include(o => o.BuyOrder).ThenInclude(b => b.CardInfo)
                .Where(o => (o.Status == BuyOfferStatus.Accepted && o.DropOffBy <= nowUtc)
                            || (o.Status == BuyOfferStatus.Proposed && o.CreatedAt <= nowUtc - ProposalLifetime))
                .ToListAsync(ct);
            foreach (var o in expired)
            {
                var accepted = o.Status == BuyOfferStatus.Accepted;
                await CloseAsync(o, BuyOfferStatus.Expired, nowUtc, releasePending: accepted);
                await NotifyAsync(o.UserId, "Proposta scaduta", accepted
                    ? $"La consegna di {o.BuyOrder.CardInfo.Name} a {o.BuyOrder.Store.Name} non è avvenuta in tempo: la proposta è scaduta."
                    : $"{o.BuyOrder.Store.Name} non ha risposto alla proposta per {o.BuyOrder.CardInfo.Name}: è scaduta.", o.Id, "BuyOffer");
            }

            await _db.StoreBuyOrders.Where(o => o.IsActive && o.ExpiresAt != null && o.ExpiresAt <= nowUtc)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsActive, false), ct);
            return (remind.Count, expired.Count);
        }

        /// <summary>
        /// Avvisi notturni: chi ha in collezione carte richieste da un negozio vicino riceve un solo messaggio
        /// per negozio, al massimo una volta a settimana. Solo adulti; preferenza "Proposte dai negozi vicini".
        /// </summary>
        public async Task<int> SendAlertsAsync(DateTime nowUtc, INotificationDispatcher dispatcher, CancellationToken ct = default)
        {
            var orders = await _db.StoreBuyOrders.AsNoTracking().Include(o => o.Store).Include(o => o.CardInfo)
                .Where(o => o.IsActive && o.Store.Status == StoreStatus.Verified && (o.ExpiresAt == null || o.ExpiresAt > nowUtc)
                            && o.QuantityWanted - o.QuantityFilled - o.QuantityPending > 0)
                .ToListAsync(ct);
            var payloads = new List<NotificationPayload>();
            var alerts = new List<StoreBuyAlert>();
            foreach (var storeGroup in orders.GroupBy(o => o.StoreId))
            {
                var store = storeGroup.First().Store;
                if (!await StoreHasPlanAsync(store, nowUtc)) continue;
                var infoIds = storeGroup.Select(o => o.CardInfoId).Distinct().ToList();
                var weekAgo = nowUtc - AlertInterval;
                var recentlyAlerted = (await _db.StoreBuyAlerts.Where(a => a.StoreId == store.Id && a.SentAt >= weekAgo).Select(a => a.UserId).ToListAsync(ct)).ToHashSet();
                var blocked = await _blocks.GetBlockedUserIdsAsync(store.OwnerId);
                var cards = await _db.Cards.AsNoTracking().Include(c => c.User).ThenInclude(u => u.Location)
                    .Where(c => infoIds.Contains(c.CardInfoId) && c.UserId != store.OwnerId && c.User.IsActive)
                    .ToListAsync(ct);
                foreach (var byUser in cards.GroupBy(c => c.UserId))
                {
                    var user = byUser.First().User;
                    if (recentlyAlerted.Contains(user.Id) || blocked.Contains(user.Id) || !IsAdult(user, nowUtc)) continue;
                    if (user.Location?.Latitude == null || user.Location.Longitude == null) continue;
                    var radius = user.Location.MaxDistanceKm > 0 ? user.Location.MaxDistanceKm : DefaultRadiusKm;
                    if (Geo.DistanceKm((double)user.Location.Latitude, (double)user.Location.Longitude, (double)store.Latitude, (double)store.Longitude) > radius) continue;
                    var wanted = storeGroup.Where(o => byUser.Any(c => Matches(c, o))).ToList();
                    if (wanted.Count == 0) continue;
                    var parts = wanted.Take(3).Select(o => $"{o.CardInfo.Name} ({PriceText(o)})");
                    var title = wanted.Count == 1 ? $"{store.Name} compra una tua carta" : $"{store.Name} compra {wanted.Count} tue carte";
                    payloads.Add(new NotificationPayload
                    {
                        UserId = user.Id, Type = NotificationType.StoreOffer, Title = title,
                        Body = string.Join(", ", parts) + (wanted.Count > 3 ? "…" : "") + ". Proponile dalla tua collezione e portale in negozio.",
                        ReferenceId = store.Id, ReferenceType = "BuyOrders"
                    });
                    alerts.Add(new StoreBuyAlert { StoreId = store.Id, UserId = user.Id, SentAt = nowUtc });
                }
            }
            if (payloads.Count == 0) return 0;
            var delivered = await dispatcher.DeliverManyAsync(payloads);
            _db.StoreBuyAlerts.AddRange(alerts);
            await _db.SaveChangesAsync(ct);
            return delivered;
        }

        private async Task NotifyAsync(int userId, string title, string body, int refId, string refType)
        {
            try
            {
                await _notifications.SendAsync(userId, NotificationType.StoreActivity, title, body, refId, refType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notifica lista acquisti non inviata a {UserId}", userId);
            }
        }
    }
}
