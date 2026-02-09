using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Repositories
{
    public class TradeOfferRepository : BaseRepository<TradeOffer>, ITradeOfferRepository
    {
        public TradeOfferRepository(ApplicationDbContext context) : base(context) { }

        public async Task<TradeOffer?> GetWithDetailsAsync(int id)
        {
            return await _dbSet
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.Items)
                    .ThenInclude(i => i.Card)
                        .ThenInclude(c => c.CardInfo)
                            .ThenInclude(ci => ci.CardSet)
                .Include(t => t.Reviews)
                    .ThenInclude(r => r.Reviewer)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<IEnumerable<TradeOffer>> GetUserOffersAsync(int userId, TradeOfferStatus? status = null)
        {
            var query = _dbSet
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.Items)
                    .ThenInclude(i => i.Card)
                        .ThenInclude(c => c.CardInfo)
                .Where(t => t.SenderId == userId || t.ReceiverId == userId);

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        }

        public async Task<(IEnumerable<TradeOffer> Items, int TotalCount)> GetUserOffersPagedAsync(
            int userId, int page, int pageSize, TradeOfferStatus? status = null)
        {
            var query = _dbSet
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.Items)
                    .ThenInclude(i => i.Card)
                        .ThenInclude(c => c.CardInfo)
                .Where(t => t.SenderId == userId || t.ReceiverId == userId);

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<int> GetActiveOfferCountAsync(int userId)
        {
            return await _dbSet.CountAsync(t =>
                t.SenderId == userId &&
                t.Status == TradeOfferStatus.Pending &&
                !t.IsDeleted);
        }
    }
}
