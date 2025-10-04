using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Repositories
{
    public class CardRepository : BaseRepository<Card>, ICardRepository
    {
        public CardRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Card>> GetUserCardsAsync(int userId)
        {
            return await _dbSet
                .Include(c => c.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Where(c => c.UserId == userId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Card>> GetCardsByCardInfoAsync(int cardInfoId)
        {
            return await _dbSet
                .Include(c => c.User)
                    .ThenInclude(u => u.Location)
                .Where(c => c.CardInfoId == cardInfoId && c.IsAvailableForTrade)
                .ToListAsync();
        }

        public async Task<IEnumerable<Card>> GetAvailableCardsAsync()
        {
            return await _dbSet
                .Include(c => c.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Include(c => c.User)
                    .ThenInclude(u => u.Location)
                .Where(c => c.IsAvailableForTrade)
                .ToListAsync();
        }

        public async Task<IEnumerable<Card>> SearchCardsAsync(string searchTerm)
        {
            var lowerSearchTerm = searchTerm.ToLower();

            return await _dbSet
                .Include(c => c.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Include(c => c.User)
                    .ThenInclude(u => u.Location)
                .Where(c => c.IsAvailableForTrade &&
                           (c.CardInfo.Name.ToLower().Contains(lowerSearchTerm) ||
                            c.CardInfo.CardSet.Name.ToLower().Contains(lowerSearchTerm) ||
                            c.CardInfo.CardSet.Game.Name.ToLower().Contains(lowerSearchTerm)))
                .ToListAsync();
        }

        public async Task<IEnumerable<Card>> GetCardsByLocationAsync(string city, string province, string country)
        {
            return await _dbSet
                .Include(c => c.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Include(c => c.User)
                    .ThenInclude(u => u.Location)
                .Where(c => c.IsAvailableForTrade &&
                           c.User.Location != null &&
                           c.User.Location.City.ToLower() == city.ToLower() &&
                           c.User.Location.Province.ToLower() == province.ToLower() &&
                           c.User.Location.Country.ToLower() == country.ToLower())
                .ToListAsync();
        }

        public async Task<IEnumerable<Card>> GetCardsByConditionAsync(CardCondition condition)
        {
            return await _dbSet
                .Include(c => c.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Include(c => c.User)
                    .ThenInclude(u => u.Location)
                .Where(c => c.IsAvailableForTrade && c.Condition == condition)
                .ToListAsync();
        }

        public async Task<Card?> GetUserCardAsync(int userId, int cardId)
        {
            return await _dbSet
                .Include(c => c.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId);
        }
    }
}