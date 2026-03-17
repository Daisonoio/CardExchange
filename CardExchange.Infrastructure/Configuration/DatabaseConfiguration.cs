using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CardExchange.Infrastructure.Configuration
{
    public static class DatabaseConfiguration
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' non configurata.");

            // Check for SQL Server-specific keywords to properly detect the provider
            var isSqlServer = connectionString.Contains("Initial Catalog", StringComparison.OrdinalIgnoreCase) ||
                              connectionString.Contains("Integrated Security", StringComparison.OrdinalIgnoreCase) ||
                              connectionString.Contains("User ID", StringComparison.OrdinalIgnoreCase) ||
                              connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                              connectionString.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase);

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                if (isSqlServer)
                {
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    });
                }
                else
                {
                    // Assume SQLite for other connection strings (typically "Data Source=filename.db")
                    options.UseSqlite(connectionString);
                }

                options.EnableSensitiveDataLogging(false)
                       .EnableDetailedErrors(false);
            });

            return services;
        }
    }
}