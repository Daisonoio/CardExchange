using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CardExchange.Infrastructure.Configuration
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            // Registrazione Repository
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ICardRepository, CardRepository>();
            services.AddScoped<ICardInfoRepository, CardInfoRepository>();
            services.AddScoped<IWishlistRepository, WishlistRepository>();
            services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));

            return services;
        }
    }
}