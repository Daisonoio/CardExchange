using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Repositories
{
    public class WishlistRepository : BaseRepository<WishlistItem>, IWishlistRepository
    {
        public WishlistRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<WishlistItem>> GetUserWishlistAsync(int userId)
        {
            return await _dbSet
                .Include(wi => wi.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Where(wi => wi.UserId == userId)
                .OrderBy(wi => wi.Priority)
                .ThenBy(wi => wi.CardInfo.Name)
                .ToListAsync();
        }

        public async Task<WishlistItem?> GetUserWishlistItemAsync(int userId, int cardInfoId)
        {
            return await _dbSet
                .Include(wi => wi.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .FirstOrDefaultAsync(wi => wi.UserId == userId && wi.CardInfoId == cardInfoId);
        }

        public async Task<IEnumerable<WishlistItem>> GetWishlistByCardInfoAsync(int cardInfoId)
        {
            return await _dbSet
                .Include(wi => wi.User)
                    .ThenInclude(u => u.Location)
                .Include(wi => wi.CardInfo)
                .Where(wi => wi.CardInfoId == cardInfoId)
                .OrderBy(wi => wi.Priority)
                .ToListAsync();
        }

        public async Task<IEnumerable<WishlistItem>> GetWishlistByPriorityAsync(int userId, int priority)
        {
            return await _dbSet
                .Include(wi => wi.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Where(wi => wi.UserId == userId && wi.Priority == priority)
                .OrderBy(wi => wi.CardInfo.Name)
                .ToListAsync();
        }
    }
}