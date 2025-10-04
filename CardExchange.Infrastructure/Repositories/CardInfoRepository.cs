using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Repositories
{
    public class CardInfoRepository : BaseRepository<CardInfo>, ICardInfoRepository
    {
        public CardInfoRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<CardInfo>> GetByCardSetAsync(int cardSetId)
        {
            return await _dbSet
                .Include(ci => ci.CardSet)
                    .ThenInclude(cs => cs.Game)
                .Where(ci => ci.CardSetId == cardSetId)
                .OrderBy(ci => ci.Name)
                .ToListAsync();
        }

        public async Task<CardInfo?> GetByNameAndSetAsync(string name, int cardSetId)
        {
            return await _dbSet
                .Include(ci => ci.CardSet)
                    .ThenInclude(cs => cs.Game)
                .FirstOrDefaultAsync(ci => ci.Name.ToLower() == name.ToLower() &&
                                          ci.CardSetId == cardSetId);
        }

        public async Task<IEnumerable<CardInfo>> SearchByNameAsync(string name)
        {
            var lowerName = name.ToLower();

            return await _dbSet
                .Include(ci => ci.CardSet)
                    .ThenInclude(cs => cs.Game)
                .Where(ci => ci.Name.ToLower().Contains(lowerName))
                .OrderBy(ci => ci.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<CardInfo>> GetPopularCardsAsync(int count = 10)
        {
            // Carte più popolari basate su quante volte sono presenti nelle collezioni
            return await _dbSet
                .Include(ci => ci.CardSet)
                    .ThenInclude(cs => cs.Game)
                .OrderByDescending(ci => ci.Cards.Count)
                .Take(count)
                .ToListAsync();
        }
    }
}