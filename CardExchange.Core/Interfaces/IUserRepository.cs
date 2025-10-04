using CardExchange.Core.Entities;

namespace CardExchange.Core.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetWithLocationAsync(int id);
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<IEnumerable<User>> GetUsersByLocationAsync(string city, string province, string country);
        Task<IEnumerable<User>> GetUsersInRadiusAsync(decimal latitude, decimal longitude, int radiusKm);
        Task<bool> EmailExistsAsync(string email);
        Task<bool> UsernameExistsAsync(string username);
        Task<User> CreateAsync(User user);
        void Update(User user);
        void Delete(User user);
        Task<bool> SaveChangesAsync();
    }
}