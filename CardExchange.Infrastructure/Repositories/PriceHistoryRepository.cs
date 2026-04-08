using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Repositories
{
    public class PriceHistoryRepository : BaseRepository<PriceHistory>, IPriceHistoryRepository
    {
        public PriceHistoryRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<PriceHistory>> GetHistoryForCardAsync(int cardInfoId, int days = 90)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            return await _dbSet
                .Where(ph => ph.CardInfoId == cardInfoId && ph.SnapshotDate >= since)
                .OrderBy(ph => ph.SnapshotDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<PriceHistory?> GetLatestSnapshotAsync(int cardInfoId)
        {
            return await _dbSet
                .Where(ph => ph.CardInfoId == cardInfoId)
                .OrderByDescending(ph => ph.SnapshotDate)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<PriceHistory>> GetLatestSnapshotsAsync(IEnumerable<int> cardInfoIds)
        {
            var ids = cardInfoIds.ToList();

            // Prendi l'ultimo snapshot per ogni cardInfoId
            return await _dbSet
                .Where(ph => ids.Contains(ph.CardInfoId))
                .GroupBy(ph => ph.CardInfoId)
                .Select(g => g.OrderByDescending(ph => ph.SnapshotDate).First())
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task SaveSnapshotAsync(int cardInfoId, decimal? priceUsd, decimal? priceUsdFoil, decimal? priceEur, decimal? priceEurFoil)
        {
            var today = DateTime.UtcNow.Date;

            // Evita duplicati nello stesso giorno
            var existing = await _dbSet
                .FirstOrDefaultAsync(ph => ph.CardInfoId == cardInfoId && ph.SnapshotDate == today);

            if (existing != null)
            {
                existing.PriceUsd = priceUsd;
                existing.PriceUsdFoil = priceUsdFoil;
                existing.PriceEur = priceEur;
                existing.PriceEurFoil = priceEurFoil;
                _dbSet.Update(existing);
            }
            else
            {
                await _dbSet.AddAsync(new PriceHistory
                {
                    CardInfoId = cardInfoId,
                    PriceUsd = priceUsd,
                    PriceUsdFoil = priceUsdFoil,
                    PriceEur = priceEur,
                    PriceEurFoil = priceEurFoil,
                    SnapshotDate = today
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<bool> HasSnapshotForDateAsync(int cardInfoId, DateTime date)
        {
            return await _dbSet.AnyAsync(ph => ph.CardInfoId == cardInfoId && ph.SnapshotDate == date.Date);
        }

        public async Task<IEnumerable<PriceHistory>> GetHistoryForCardsAsync(IEnumerable<int> cardInfoIds, int days)
        {
            var ids = cardInfoIds.ToList();
            var since = DateTime.UtcNow.AddDays(-days);
            return await _dbSet
                .Where(ph => ids.Contains(ph.CardInfoId) && ph.SnapshotDate >= since)
                .OrderBy(ph => ph.SnapshotDate)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
