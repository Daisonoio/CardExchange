using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<User?> GetWithLocationAsync(int id)
        {
            return await _context.Users
                .Include(u => u.Location)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _context.Users
                .Include(u => u.Location)
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetUsersByLocationAsync(string city, string province, string country)
        {
            return await _context.Users
                .Include(u => u.Location)
                .Where(u => u.Location != null &&
                           u.Location.City.ToLower() == city.ToLower() &&
                           u.Location.Province.ToLower() == province.ToLower() &&
                           u.Location.Country.ToLower() == country.ToLower())
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetUsersInRadiusAsync(decimal latitude, decimal longitude, int radiusKm)
        {
            // Implementazione semplificata del calcolo della distanza
            // In produzione si potrebbe usare una funzione SQL più precisa
            var latRange = radiusKm / 111.0m; // Approssimativamente 1 grado = 111 km
            var lngRange = radiusKm / (111.0m * (decimal)Math.Cos((double)latitude * Math.PI / 180));

            return await _context.Users
                .Include(u => u.Location)
                .Where(u => u.Location != null &&
                           u.Location.Latitude != null && u.Location.Longitude != null &&
                           u.Location.Latitude >= latitude - latRange &&
                           u.Location.Latitude <= latitude + latRange &&
                           u.Location.Longitude >= longitude - lngRange &&
                           u.Location.Longitude <= longitude + lngRange)
                .ToListAsync();
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Users
                .AnyAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            return await _context.Users
                .AnyAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<User> CreateAsync(User user)
        {
            await _context.Users.AddAsync(user);
            return user;
        }

        public void Update(User user)
        {
            _context.Users.Update(user);
        }

        public void Delete(User user)
        {
            user.IsActive = false;
            _context.Users.Update(user);
        }

        public async Task<bool> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}