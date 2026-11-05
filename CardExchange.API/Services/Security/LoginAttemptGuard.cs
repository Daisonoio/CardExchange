using Microsoft.Extensions.Caching.Memory;

namespace CardExchange.API.Services.Security
{
    /// <summary>
    /// Protezione per account contro i tentativi di indovinare la password: dopo 5 errori in 15 minuti
    /// l'account (email o username) è bloccato per 15 minuti, da qualunque indirizzo arrivino i tentativi.
    /// Così il limite per indirizzo IP può essere largo, utile quando molte persone condividono la stessa
    /// rete (il Wi-Fi di un negozio durante una serata di scambio).
    /// </summary>
    public class LoginAttemptGuard
    {
        public const int MaxFailures = 5;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        private readonly IMemoryCache _cache;
        private readonly object _gate = new();

        public LoginAttemptGuard(IMemoryCache cache) => _cache = cache;

        private static string Key(string login) => $"login-failures:{login.Trim().ToLowerInvariant()}";

        /// <summary>Minuti di attesa se l'account è bloccato, altrimenti null.</summary>
        public int? LockedForMinutes(string login, DateTime now)
        {
            if (!_cache.TryGetValue(Key(login), out Failures? f) || f == null) return null;
            lock (_gate)
            {
                f.Prune(now);
                if (f.Times.Count < MaxFailures) return null;
                var until = f.Times[0] + Window;
                return until > now ? Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes)) : null;
            }
        }

        public void RecordFailure(string login, DateTime now)
        {
            var f = _cache.GetOrCreate(Key(login), e => { e.SlidingExpiration = Window; return new Failures(); })!;
            lock (_gate)
            {
                f.Prune(now);
                f.Times.Add(now);
            }
        }

        public void Reset(string login) => _cache.Remove(Key(login));

        private sealed class Failures
        {
            public List<DateTime> Times { get; } = new();
            public void Prune(DateTime now) => Times.RemoveAll(t => now - t > Window);
        }
    }
}
