using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Moderation
{
    public interface IUserBlockService
    {
        /// <summary>Utenti bloccati da <paramref name="userId"/> o che hanno bloccato <paramref name="userId"/>.</summary>
        Task<HashSet<int>> GetBlockedUserIdsAsync(int userId);

        /// <summary>True se uno dei due ha bloccato l'altro.</summary>
        Task<bool> IsBlockedAsync(int userA, int userB);
    }

    /// <summary>Il blocco vale in entrambe le direzioni. Risultati memorizzati per la durata della richiesta.</summary>
    public class UserBlockService : IUserBlockService
    {
        private readonly ApplicationDbContext _context;
        private readonly Dictionary<int, HashSet<int>> _cache = new();

        public UserBlockService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<HashSet<int>> GetBlockedUserIdsAsync(int userId)
        {
            if (userId == 0) return new HashSet<int>();
            if (_cache.TryGetValue(userId, out var cached)) return cached;

            var pairs = await _context.UserBlocks
                .AsNoTracking()
                .Where(b => b.BlockerId == userId || b.BlockedUserId == userId)
                .Select(b => new { b.BlockerId, b.BlockedUserId })
                .ToListAsync();

            var result = pairs.Select(p => p.BlockerId == userId ? p.BlockedUserId : p.BlockerId).ToHashSet();
            _cache[userId] = result;
            return result;
        }

        public async Task<bool> IsBlockedAsync(int userA, int userB) =>
            (await GetBlockedUserIdsAsync(userA)).Contains(userB);
    }
}
