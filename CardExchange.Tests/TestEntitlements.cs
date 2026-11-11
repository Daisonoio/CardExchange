using CardExchange.API.Configuration;
using CardExchange.API.Services.Entitlements;
using CardExchange.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CardExchange.Tests;

/// <summary>Servizio dei diritti con i piani a pagamento accesi: per i test della logica dei piani (tenuta per il futuro).</summary>
public static class TestEntitlements
{
    public static IOptions<FeatureOptions> PaidOptions => Options.Create(new FeatureOptions { PaidPlans = true });

    public static EntitlementService Paid(ApplicationDbContext db, IMemoryCache? cache = null) =>
        new(db, cache ?? new MemoryCache(new MemoryCacheOptions()), null, PaidOptions);
}
