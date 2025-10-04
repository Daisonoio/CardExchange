using CardExchange.Core.Entities;

namespace CardExchange.Core.Interfaces
{
    public interface IWishlistRepository : IBaseRepository<WishlistItem>
    {
        Task<IEnumerable<WishlistItem>> GetUserWishlistAsync(int userId);
        Task<WishlistItem?> GetUserWishlistItemAsync(int userId, int cardInfoId);
        Task<IEnumerable<WishlistItem>> GetWishlistByCardInfoAsync(int cardInfoId);
        Task<IEnumerable<WishlistItem>> GetWishlistByPriorityAsync(int userId, int priority);
    }
}