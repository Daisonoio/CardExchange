using CardExchange.API.Services.Entitlements;
using System.Security.Cryptography;
using CardExchange.API.Services.Moderation;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Stores
{
    public class ReservationException : Exception
    {
        public ReservationException(string message) : base(message) { }
    }

    /// <summary>
    /// "Prenota e ritira": blocco della quantità senza doppie assegnazioni, ritiro con codice in cassa,
    /// scadenza automatica, limiti per utente e pausa dopo le mancate presentazioni. Si paga in negozio.
    /// </summary>
    public class ReservationService
    {
        public const int MaxActive = 3;
        public const int MaxActivePremium = 5;
        public const int NoShowLimit = 2;
        public static readonly TimeSpan NoShowWindow = TimeSpan.FromDays(90);
        public static readonly TimeSpan NoShowPause = TimeSpan.FromDays(30);
        /// <summary>Ore di apertura entro cui ritirare (la domenica non conta).</summary>
        public const int HoldHours = 48;
        public static readonly TimeSpan ReminderBefore = TimeSpan.FromHours(6);
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static readonly ReservationStatus[] ActiveStatuses = { ReservationStatus.Pending, ReservationStatus.Confirmed };

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly IUserBlockService _blocks;
        private readonly ILogger<ReservationService> _logger;

        private readonly IEntitlementService _entitlements;

        public ReservationService(ApplicationDbContext db, INotificationService notifications, IUserBlockService blocks, ILogger<ReservationService> logger,
            IEntitlementService? entitlements = null)
        {
            _entitlements = entitlements.OrDefault(db);
            _db = db;
            _notifications = notifications;
            _blocks = blocks;
            _logger = logger;
        }

        /// <summary>48 ore a partire da adesso, senza contare le domeniche (ora italiana).</summary>
        public static DateTime ExpiryFrom(DateTime nowUtc)
        {
            var zone = TimeZones.Italy;
            var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
            var remaining = TimeSpan.FromHours(HoldHours);
            var cursor = local;
            while (remaining > TimeSpan.Zero)
            {
                var nextMidnight = cursor.Date.AddDays(1);
                var chunk = nextMidnight - cursor;
                if (cursor.DayOfWeek == DayOfWeek.Sunday) { cursor = nextMidnight; continue; }
                if (chunk >= remaining) { cursor += remaining; remaining = TimeSpan.Zero; }
                else { remaining -= chunk; cursor = nextMidnight; }
            }
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(cursor, DateTimeKind.Unspecified), zone);
        }

        public static string NewCode() =>
            new string(Enumerable.Range(0, 6).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]).ToArray());

        /// <summary>Il negozio accetta prenotazioni: verificato e con il piano Negozio Pro.</summary>
        public async Task<bool> StoreAcceptsReservationsAsync(Store store, DateTime nowUtc)
        {
            if (store.Status != StoreStatus.Verified) return false;
            return await _entitlements.HasAsync(store.OwnerId, Capability.Reservations);
        }


        public async Task<Reservation> CreateAsync(int storeId, int userId, int cardId, int quantity, DateTime nowUtc)
        {
            if (quantity < 1) throw new ReservationException("La quantità deve essere almeno 1");
            var store = await _db.Stores.Include(s => s.Owner).FirstOrDefaultAsync(s => s.Id == storeId)
                        ?? throw new ReservationException("Negozio non trovato");
            if (store.OwnerId == userId) throw new ReservationException("Non puoi prenotare le carte del tuo negozio");
            if (!await StoreAcceptsReservationsAsync(store, nowUtc))
                throw new ReservationException("Questo negozio non accetta prenotazioni");
            if (await _blocks.IsBlockedAsync(userId, store.OwnerId))
                throw new ReservationException("Non puoi prenotare in questo negozio");

            var noShows = await _db.Reservations.Where(r => r.UserId == userId && r.Status == ReservationStatus.NoShow && r.ClosedAt >= nowUtc - NoShowWindow)
                .OrderByDescending(r => r.ClosedAt).Select(r => r.ClosedAt!.Value).ToListAsync();
            if (noShows.Count >= NoShowLimit && noShows[0] + NoShowPause > nowUtc)
                throw new ReservationException($"Dopo {NoShowLimit} prenotazioni non ritirate puoi prenotare di nuovo dal {TimeZones.ToLocal(noShows[0] + NoShowPause, null):dd/MM/yyyy}");

            var active = await _db.Reservations.CountAsync(r => r.UserId == userId && ActiveStatuses.Contains(r.Status));
            var max = await _entitlements.LimitAsync(userId, PlanLimit.ActiveReservations);
            if (active >= max) throw new ReservationException($"Hai già {max} prenotazioni attive: ritirale o annullane una");

            var card = await _db.Cards.AsNoTracking().Include(c => c.CardInfo).FirstOrDefaultAsync(c => c.Id == cardId)
                       ?? throw new ReservationException("Carta non trovata");
            if (card.UserId != store.OwnerId || !card.IsAvailableForTrade)
                throw new ReservationException("Questa carta non è disponibile in negozio");
            var early = await EarlyAccess.HiddenAsync(_entitlements, userId, new[] { (card.Id, card.UserId, card.CreatedAt) }, nowUtc);
            if (early.Count > 0)
                throw new ReservationException($"Carta appena arrivata: per {EarlyAccess.MinutesLeft(card.CreatedAt, nowUtc)} minuti la possono prenotare solo i Premium");

            var reservation = await _db.InTransactionAsync(async () =>
            {
                // Un solo aggiornamento condizionale: se nel frattempo qualcuno ha preso l'ultima copia, non tocca nulla
                var locked = await _db.Cards
                    .Where(c => c.Id == cardId && c.IsAvailableForTrade && c.Quantity - c.ReservedQuantity >= quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.ReservedQuantity, c => c.ReservedQuantity + quantity));
                if (locked == 0) throw new ReservationException("Questa carta non è più disponibile");
                var r = new Reservation
                {
                    StoreId = storeId, CardId = cardId, UserId = userId, Quantity = quantity,
                    PriceEur = CardPricing.EffectivePriceEur(card), Status = ReservationStatus.Pending,
                    ExpiresAt = ExpiryFrom(nowUtc), PickupCode = NewCode()
                };
                _db.Reservations.Add(r);
                await _db.SaveChangesAsync();
                return r;
            });

            var who = await _db.Users.Where(u => u.Id == userId).Select(u => u.Username).FirstAsync();
            await NotifyAsync(store.OwnerId, "Nuova prenotazione", $"@{who} ha prenotato {Name(card)} (x{quantity}): preparala per il ritiro.", store.Id, "StoreReservations");
            return reservation;
        }

        private static string Name(Card c) => c.CardInfo?.Name ?? "una carta";

        private async Task<Reservation> LoadAsync(int id) =>
            await _db.Reservations.Include(r => r.Store).Include(r => r.Card).ThenInclude(c => c.CardInfo).Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id) ?? throw new ReservationException("Prenotazione non trovata");

        private static void RequireOwner(Reservation r, int actingUserId)
        {
            if (r.Store.OwnerId != actingUserId) throw new UnauthorizedAccessException();
        }

        private static void RequireActive(Reservation r)
        {
            if (!ActiveStatuses.Contains(r.Status)) throw new ReservationException("Questa prenotazione non è più attiva");
        }

        /// <summary>Rilascia le copie bloccate (mai sotto zero).</summary>
        private Task ReleaseAsync(int cardId, int quantity) =>
            _db.Cards.IgnoreQueryFilters().Where(c => c.Id == cardId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ReservedQuantity, c => c.ReservedQuantity >= quantity ? c.ReservedQuantity - quantity : 0));

        public async Task<Reservation> ConfirmAsync(int id, int ownerId, DateTime nowUtc)
        {
            var r = await LoadAsync(id);
            RequireOwner(r, ownerId);
            if (r.Status != ReservationStatus.Pending) throw new ReservationException("Questa prenotazione non è in attesa di conferma");
            r.Status = ReservationStatus.Confirmed;
            r.ConfirmedAt = nowUtc;
            await _db.SaveChangesAsync();
            var tz = r.User.TimeZone;
            await NotifyAsync(r.UserId, "Prenotazione confermata",
                $"{r.Store.Name} ha preparato {Name(r.Card)}. Ritirala entro {TimeZones.FormatWhen(r.ExpiresAt, tz)} in {r.Store.Address}, {r.Store.City} con il codice {r.PickupCode}. Si paga in negozio.",
                r.Id, "Reservation");
            return r;
        }

        /// <summary>Ritiro in cassa: il codice deve corrispondere; la copia esce dalla quantità del negozio.</summary>
        public async Task<Reservation> PickUpAsync(int id, int ownerId, string code, DateTime nowUtc)
        {
            var r = await LoadAsync(id);
            RequireOwner(r, ownerId);
            RequireActive(r);
            if (!string.Equals(code?.Trim(), r.PickupCode, StringComparison.OrdinalIgnoreCase))
                throw new ReservationException("Il codice di ritiro non corrisponde");
            await _db.InTransactionAsync(async () =>
            {
                await _db.Cards.IgnoreQueryFilters().Where(c => c.Id == r.CardId).ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.ReservedQuantity, c => c.ReservedQuantity >= r.Quantity ? c.ReservedQuantity - r.Quantity : 0)
                    .SetProperty(c => c.Quantity, c => c.Quantity >= r.Quantity ? c.Quantity - r.Quantity : 0));
                // Esaurita: la carta non è più in vendita (resta nello storico delle prenotazioni)
                await _db.Cards.Where(c => c.Id == r.CardId && c.Quantity <= 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsAvailableForTrade, false).SetProperty(c => c.IsDeleted, true));
                r.Status = ReservationStatus.PickedUp;
                r.ClosedAt = nowUtc;
                await _db.SaveChangesAsync();
            });
            return r;
        }

        public async Task<Reservation> CancelAsync(int id, int actingUserId, DateTime nowUtc)
        {
            var r = await LoadAsync(id);
            var byStore = r.Store.OwnerId == actingUserId;
            if (!byStore && r.UserId != actingUserId) throw new UnauthorizedAccessException();
            RequireActive(r);
            await CloseAsync(r, ReservationStatus.Cancelled, nowUtc);
            if (byStore)
                await NotifyAsync(r.UserId, "Prenotazione annullata", $"{r.Store.Name} ha annullato la prenotazione di {Name(r.Card)}.", r.Id, "Reservation");
            else
                await NotifyAsync(r.Store.OwnerId, "Prenotazione annullata", $"@{r.User.Username} ha annullato la prenotazione di {Name(r.Card)}.", r.StoreId, "StoreReservations");
            return r;
        }

        public async Task<Reservation> NoShowAsync(int id, int ownerId, DateTime nowUtc)
        {
            var r = await LoadAsync(id);
            RequireOwner(r, ownerId);
            RequireActive(r);
            await CloseAsync(r, ReservationStatus.NoShow, nowUtc);
            return r;
        }

        private async Task CloseAsync(Reservation r, ReservationStatus status, DateTime nowUtc)
        {
            await _db.InTransactionAsync(async () =>
            {
                await ReleaseAsync(r.CardId, r.Quantity);
                r.Status = status;
                r.ClosedAt = nowUtc;
                await _db.SaveChangesAsync();
            });
        }

        /// <summary>Ogni 15 minuti: promemoria 6 ore prima della scadenza, poi scadenza e rilascio della quantità.</summary>
        public async Task<(int Reminded, int Expired)> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var remind = await _db.Reservations.Include(r => r.Store).Include(r => r.Card).ThenInclude(c => c.CardInfo).Include(r => r.User)
                .Where(r => ActiveStatuses.Contains(r.Status) && r.ReminderSentAt == null && r.ExpiresAt > nowUtc && r.ExpiresAt <= nowUtc + ReminderBefore)
                .ToListAsync(ct);
            foreach (var r in remind)
            {
                r.ReminderSentAt = nowUtc;
                await NotifyAsync(r.UserId, "Ricordati il ritiro",
                    $"{Name(r.Card)} ti aspetta da {r.Store.Name} fino a {TimeZones.FormatWhen(r.ExpiresAt, r.User.TimeZone)}. Codice: {r.PickupCode}.", r.Id, "Reservation");
            }
            if (remind.Count > 0) await _db.SaveChangesAsync(ct);

            var expired = await _db.Reservations.Include(r => r.Store).Include(r => r.Card).ThenInclude(c => c.CardInfo)
                .Where(r => ActiveStatuses.Contains(r.Status) && r.ExpiresAt <= nowUtc)
                .ToListAsync(ct);
            foreach (var r in expired)
            {
                await CloseAsync(r, ReservationStatus.Expired, nowUtc);
                await NotifyAsync(r.UserId, "Prenotazione scaduta", $"La prenotazione di {Name(r.Card)} da {r.Store.Name} è scaduta e la carta è tornata disponibile.", r.Id, "Reservation");
            }
            return (remind.Count, expired.Count);
        }

        private async Task NotifyAsync(int userId, string title, string body, int refId, string refType)
        {
            try
            {
                await _notifications.SendAsync(userId, NotificationType.StoreActivity, title, body, refId, refType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notifica prenotazione non inviata a {UserId}", userId);
            }
        }
    }
}
