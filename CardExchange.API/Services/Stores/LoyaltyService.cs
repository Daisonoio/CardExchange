using System.Security.Cryptography;
using System.Text;
using CardExchange.API.Configuration;
using CardExchange.API.Services.Entitlements;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Services.Stores
{
    public class LoyaltyException : Exception
    {
        public int Status { get; }
        public string Code { get; }
        public LoyaltyException(string message, string code = "LOYALTY", int status = 400) : base(message) { Code = code; Status = status; }
    }

    public enum StampOutcome { Stamped, AlreadyToday, NoProgram, Skipped }

    public record StampResult(StampOutcome Outcome, int Stamps, int StampsRequired, bool RewardReady, string? Username = null);

    /// <summary>
    /// Pass della tessera: un codice firmato con l'utente e la scadenza (2 minuti), da mostrare in negozio come QR.
    /// Breve abbastanza da essere digitato a mano se la fotocamera non legge il QR.
    /// Formato: {userId in base 36}-{scadenza unix in base 36}-{firma, 10 caratteri}.
    /// </summary>
    public static class LoyaltyPass
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);
        private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        private const string SigAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Create(int userId, DateTime nowUtc, string key)
        {
            var exp = new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc) + Lifetime).ToUnixTimeSeconds();
            var body = $"{ToBase36(userId)}-{ToBase36(exp)}";
            return $"{body}-{Sign(body, key)}";
        }

        /// <summary>L'utente del pass, oppure un errore chiaro se è scaduto o non valido.</summary>
        public static int Validate(string? token, DateTime nowUtc, string key)
        {
            var parts = (token ?? "").Trim().ToLowerInvariant().Split('-');
            if (parts.Length != 3) throw new LoyaltyException("Codice non valido", "PASS_INVALID");
            var body = $"{parts[0]}-{parts[1]}";
            var expected = Sign(body, key).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2])))
                throw new LoyaltyException("Codice non valido", "PASS_INVALID");
            var userId = (int)FromBase36(parts[0]);
            var exp = DateTimeOffset.FromUnixTimeSeconds(FromBase36(parts[1])).UtcDateTime;
            if (nowUtc > exp) throw new LoyaltyException("Il codice è scaduto: chiedi al cliente di riaprire la tessera", "PASS_EXPIRED");
            return userId;
        }

        private static string Sign(string body, string key)
        {
            var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("loyalty:" + key), Encoding.ASCII.GetBytes(body));
            // 10 caratteri da 5 bit: 50 bit di firma, più che sufficienti per un codice che vale 2 minuti
            var sb = new StringBuilder();
            ulong bits = BitConverter.ToUInt64(mac, 0);
            for (var i = 0; i < 10; i++) { sb.Append(SigAlphabet[(int)(bits & 31)]); bits >>= 5; }
            return sb.ToString();
        }

        private static string ToBase36(long v)
        {
            if (v == 0) return "0";
            var sb = new StringBuilder();
            while (v > 0) { sb.Insert(0, Alphabet[(int)(v % 36)]); v /= 36; }
            return sb.ToString();
        }

        private static long FromBase36(string s)
        {
            if (s.Length is 0 or > 12) throw new LoyaltyException("Codice non valido", "PASS_INVALID");
            long v = 0;
            foreach (var c in s)
            {
                var i = Alphabet.IndexOf(c);
                if (i < 0) throw new LoyaltyException("Codice non valido", "PASS_INVALID");
                v = v * 36 + i;
            }
            return v;
        }
    }

    /// <summary>
    /// Tessera fedeltà dei negozi: un timbro al giorno per negozio (dal personale con il QR del cliente
    /// o al check-in di un evento del negozio), premio al raggiungimento dei timbri. Il premio lo decide e lo paga il negozio.
    /// </summary>
    public class LoyaltyService
    {
        public const int MinStamps = 2;
        public const int MaxStamps = 20;
        /// <summary>Le statistiche mostrate al negozio compaiono solo da 3 clienti in su.</summary>
        public const int MinUsersForStats = 3;

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly IEntitlementService _entitlements;
        private readonly string _key;

        public LoyaltyService(ApplicationDbContext db, INotificationService notifications, IOptions<JwtSettings> jwt, IEntitlementService? entitlements = null)
        {
            _db = db;
            _notifications = notifications;
            _entitlements = entitlements.OrDefault(db);
            _key = jwt.Value.SecretKey;
        }

        public string CreatePass(int userId, DateTime nowUtc) => LoyaltyPass.Create(userId, nowUtc, _key);

        public static DateOnly DayOf(DateTime nowUtc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZones.Italy));

        public Task<bool> StoreHasPlanAsync(Store store) => _entitlements.HasAsync(store.OwnerId, Capability.Loyalty);

        /// <summary>Programma attivo e utilizzabile: negozio verificato con il piano Negozio.</summary>
        private async Task<LoyaltyProgram?> ActiveProgramAsync(Store store)
        {
            if (store.Status != StoreStatus.Verified) return null;
            var program = await _db.LoyaltyPrograms.FirstOrDefaultAsync(p => p.StoreId == store.Id && p.IsActive);
            if (program == null) return null;
            return await StoreHasPlanAsync(store) ? program : null;
        }

        public async Task<LoyaltyProgram> ConfigureAsync(Store store, int stampsRequired, string reward, bool isActive)
        {
            if (!await StoreHasPlanAsync(store)) throw new LoyaltyException("La tessera fedeltà è inclusa nel piano Negozio", "PLAN_REQUIRED", 402);
            reward = (reward ?? "").Trim();
            if (stampsRequired is < MinStamps or > MaxStamps) throw new LoyaltyException($"I timbri devono essere tra {MinStamps} e {MaxStamps}");
            if (reward.Length is < 3 or > 120) throw new LoyaltyException("Descrivi il premio (da 3 a 120 caratteri)");
            var program = await _db.LoyaltyPrograms.FirstOrDefaultAsync(p => p.StoreId == store.Id);
            if (program == null)
            {
                program = new LoyaltyProgram { StoreId = store.Id };
                _db.LoyaltyPrograms.Add(program);
            }
            program.StampsRequired = stampsRequired;
            program.RewardDescription = reward;
            program.IsActive = isActive;
            program.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return program;
        }

        private async Task<LoyaltyCard> CardForAsync(int storeId, int userId)
        {
            var card = await _db.LoyaltyCards.FirstOrDefaultAsync(c => c.StoreId == storeId && c.UserId == userId);
            if (card != null) return card;
            card = new LoyaltyCard { StoreId = storeId, UserId = userId };
            _db.LoyaltyCards.Add(card);
            try
            {
                await _db.SaveChangesAsync();
                return card;
            }
            catch (DbUpdateException)
            {
                // Creata nello stesso istante da un'altra richiesta: si usa quella
                _db.Entry(card).State = EntityState.Detached;
                return await _db.LoyaltyCards.FirstAsync(c => c.StoreId == storeId && c.UserId == userId);
            }
        }

        /// <summary>Assegna il timbro del giorno. Due scansioni nello stesso giorno danno un solo timbro.</summary>
        public async Task<StampResult> StampAsync(Store store, int userId, LoyaltyStampSource source, int? refId, DateTime nowUtc)
        {
            var program = await ActiveProgramAsync(store);
            if (program == null) return new(StampOutcome.NoProgram, 0, 0, false);
            if (userId == store.OwnerId) return new(StampOutcome.Skipped, 0, program.StampsRequired, false);

            var card = await CardForAsync(store.Id, userId);
            var day = DayOf(nowUtc);
            if (await _db.LoyaltyStamps.AnyAsync(s => s.LoyaltyCardId == card.Id && s.Day == day))
                return new(StampOutcome.AlreadyToday, card.Stamps, program.StampsRequired, card.Stamps >= program.StampsRequired);

            var stamp = new LoyaltyStamp { LoyaltyCardId = card.Id, Source = source, RefId = refId, Day = day, CreatedAt = nowUtc };
            _db.LoyaltyStamps.Add(stamp);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Indice univoco su tessera e giorno: l'altro timbro è arrivato prima
                _db.Entry(stamp).State = EntityState.Detached;
                await _db.Entry(card).ReloadAsync();
                return new(StampOutcome.AlreadyToday, card.Stamps, program.StampsRequired, card.Stamps >= program.StampsRequired);
            }

            // Incremento atomico: il conteggio resta giusto anche con un riscatto contemporaneo
            await _db.LoyaltyCards.Where(c => c.Id == card.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.Stamps, c => c.Stamps + 1).SetProperty(c => c.LastStampAt, nowUtc));
            await _db.Entry(card).ReloadAsync();

            var ready = card.Stamps >= program.StampsRequired;
            if (ready && card.Stamps == program.StampsRequired)
                await _notifications.SendAsync(userId, NotificationType.Loyalty, $"Premio pronto da {store.Name}",
                    $"Hai raccolto {program.StampsRequired} timbri: in negozio ti aspetta \"{program.RewardDescription}\".", store.Id, "Store");
            else if (!ready)
                await _notifications.SendAsync(userId, NotificationType.Loyalty, $"Timbro da {store.Name}",
                    $"Ne mancano {program.StampsRequired - card.Stamps} per \"{program.RewardDescription}\".", store.Id, "Store");

            var username = await _db.Users.Where(u => u.Id == userId).Select(u => u.Username).FirstOrDefaultAsync();
            return new(StampOutcome.Stamped, card.Stamps, program.StampsRequired, ready, username);
        }

        /// <summary>Timbro dal personale con il QR del cliente.</summary>
        public async Task<StampResult> StampWithPassAsync(Store store, string token, DateTime nowUtc)
        {
            var userId = LoyaltyPass.Validate(token, nowUtc, _key);
            if (userId == store.OwnerId) throw new LoyaltyException("Non puoi timbrare la tua tessera");
            var result = await StampAsync(store, userId, LoyaltyStampSource.StaffScan, null, nowUtc);
            if (result.Outcome == StampOutcome.NoProgram) throw new LoyaltyException("La tessera fedeltà non è attiva per questo negozio", "NO_PROGRAM");
            return result with { Username = result.Username ?? await UsernameAsync(userId) };
        }

        /// <summary>Riscatto del premio con il QR del cliente: si scalano i timbri del premio.</summary>
        public async Task<StampResult> RedeemAsync(Store store, string token, DateTime nowUtc)
        {
            var userId = LoyaltyPass.Validate(token, nowUtc, _key);
            var program = await ActiveProgramAsync(store) ?? throw new LoyaltyException("La tessera fedeltà non è attiva per questo negozio", "NO_PROGRAM");
            var required = program.StampsRequired;
            var updated = await _db.LoyaltyCards.Where(c => c.StoreId == store.Id && c.UserId == userId && c.Stamps >= required)
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.Stamps, c => c.Stamps - required)
                    .SetProperty(c => c.RewardsRedeemed, c => c.RewardsRedeemed + 1)
                    .SetProperty(c => c.LastRedeemedAt, nowUtc));
            var card = await _db.LoyaltyCards.AsNoTracking().FirstOrDefaultAsync(c => c.StoreId == store.Id && c.UserId == userId);
            if (updated == 0)
                throw new LoyaltyException(card == null
                    ? "Questo cliente non ha ancora timbri da te"
                    : $"Timbri insufficienti: {card.Stamps} su {required}", "NOT_ENOUGH");
            return new(StampOutcome.Stamped, card!.Stamps, required, card.Stamps >= required, await UsernameAsync(userId));
        }

        private Task<string?> UsernameAsync(int userId) => _db.Users.Where(u => u.Id == userId).Select(u => u.Username).FirstOrDefaultAsync();

        /// <summary>Timbro automatico al check-in di un evento del negozio (se ha la tessera attiva).</summary>
        public async Task StampForEventAsync(Event ev, int userId, DateTime nowUtc)
        {
            if (ev.StoreId == null) return;
            var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == ev.StoreId.Value);
            if (store == null) return;
            await StampAsync(store, userId, LoyaltyStampSource.EventCheckIn, ev.Id, nowUtc);
        }

        public record LoyaltyStats(int Customers, int WithTwoOrMore, int RewardsReady, int RedeemedThisMonth, int StampsThisMonth);

        /// <summary>Dati aggregati per il negozio, nessuna identità. Sotto i 3 clienti i valori non si mostrano.</summary>
        public async Task<LoyaltyStats?> StatsAsync(int storeId, int stampsRequired, DateTime nowUtc)
        {
            var cards = _db.LoyaltyCards.AsNoTracking().Where(c => c.StoreId == storeId);
            var customers = await cards.CountAsync();
            if (customers < MinUsersForStats) return null;
            var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZones.Italy);
            var monthStart = new DateOnly(local.Year, local.Month, 1);
            var monthStartUtc = TimeZoneInfo.ConvertTimeToUtc(new DateTime(local.Year, local.Month, 1), TimeZones.Italy);
            var withTwo = await cards.CountAsync(c => c.Stamps + c.RewardsRedeemed * stampsRequired >= 2);
            var ready = await cards.CountAsync(c => c.Stamps >= stampsRequired);
            var redeemed = await cards.CountAsync(c => c.LastRedeemedAt >= monthStartUtc);
            var stamps = await _db.LoyaltyStamps.AsNoTracking().CountAsync(s => s.LoyaltyCard.StoreId == storeId && s.Day >= monthStart);
            // Ogni valore mostrato riguarda almeno 3 clienti, altrimenti 0 ("meno di 3")
            static int Safe(int v) => v >= MinUsersForStats ? v : 0;
            return new(customers, Safe(withTwo), Safe(ready), Safe(redeemed), stamps);
        }
    }
}
