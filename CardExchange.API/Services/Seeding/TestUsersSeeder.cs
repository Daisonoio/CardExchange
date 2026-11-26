using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Seeding
{
    /// <summary>
    /// Utenti di prova per i test manuali: uno per ogni tipo di profilo e per i casi particolari
    /// (minorenne, età mancante, email da confermare, iscritto dal QR di un negozio).
    /// Solo in sviluppo (vedi Program.cs, chiave "Seed:TestUsers"). Crea solo ciò che manca:
    /// un utente già presente non viene toccato, così le modifiche fatte durante i test restano.
    /// Credenziali in docs/utenti-di-test.md: se si cambia qualcosa qui, aggiornare anche quel file.
    /// </summary>
    public static class TestUsersSeeder
    {
        public const string Password = "Test1234!";
        public const string StoreSlug = "negozio-test-milano";
        private const string EmailDomain = "test.cardexchange.local";

        private enum Age { Adult, Minor, Missing }

        private record Spec(
            string Username, string FirstName, string LastName, ProfileType Profile, Age Age,
            string City, string Province, double Lat, double Lon,
            string[]? Roles = null, bool EmailConfirmed = true, bool FromStoreQr = false);

        // Zone di Milano diverse, così le distanze tra utenti e dal negozio sono realistiche
        private static readonly Spec[] Users =
        {
            new("test.admin", "Alessia", "Admin", ProfileType.Collector, Age.Adult, "Milano", "MI", 45.4642, 9.1900, Roles: new[] { "SuperAdmin", "Admin" }),
            new("test.moderatore", "Matteo", "Moderatore", ProfileType.Collector, Age.Adult, "Milano", "MI", 45.4700, 9.2050, Roles: new[] { "Moderator" }),
            new("test.collezionista", "Chiara", "Collezionista", ProfileType.Collector, Age.Adult, "Milano", "MI", 45.4520, 9.1750),
            new("test.collezionista2", "Paolo", "Scambi", ProfileType.Collector, Age.Adult, "Milano", "MI", 45.4780, 9.2270),
            new("test.minorenne", "Luca", "Giovane", ProfileType.Collector, Age.Minor, "Milano", "MI", 45.4870, 9.1890),
            new("test.venditore", "Valeria", "Venditore", ProfileType.Seller, Age.Adult, "Milano", "MI", 45.4600, 9.1600),
            new("test.negozio", "Nicola", "Negozio", ProfileType.Store, Age.Adult, "Milano", "MI", 45.4580, 9.1830),
            new("test.organizzatore", "Olga", "Organizzatore", ProfileType.Organizer, Age.Adult, "Sesto San Giovanni", "MI", 45.5340, 9.2300),
            new("test.senzaeta", "Sergio", "SenzaEta", ProfileType.Collector, Age.Missing, "Milano", "MI", 45.4550, 9.2100),
            new("test.nonverificato", "Nadia", "NonVerificata", ProfileType.Collector, Age.Adult, "Monza", "MB", 45.5845, 9.2744, EmailConfirmed: false),
            new("test.daqr", "Quirino", "DalQR", ProfileType.Collector, Age.Adult, "Milano", "MI", 45.4620, 9.1780, FromStoreQr: true),
        };

        public static async Task<int> SeedAsync(ApplicationDbContext context, ILogger logger, CancellationToken ct = default)
        {
            var usernames = Users.Select(u => u.Username).ToList();
            var existing = (await context.Users.IgnoreQueryFilters()
                .Where(u => usernames.Contains(u.Username))
                .Select(u => u.Username)
                .ToListAsync(ct)).ToHashSet();
            var missing = Users.Where(u => !existing.Contains(u.Username)).ToList();

            var roles = await context.Roles.ToDictionaryAsync(r => r.Name, r => r.Id, ct);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var hash = missing.Count > 0 ? BCrypt.Net.BCrypt.HashPassword(Password) : string.Empty;
            var created = new Dictionary<string, User>();

            foreach (var spec in missing)
            {
                var user = new User
                {
                    Email = $"{spec.Username.Replace(".", "")}@{EmailDomain}",
                    Username = spec.Username,
                    FirstName = spec.FirstName,
                    LastName = spec.LastName,
                    PasswordHash = hash,
                    IsActive = true,
                    EmailConfirmed = spec.EmailConfirmed,
                    EmailVerificationToken = spec.EmailConfirmed ? null : Guid.NewGuid().ToString("N"),
                    EmailVerificationTokenExpiry = spec.EmailConfirmed ? null : DateTime.UtcNow.AddDays(30),
                    BirthDate = spec.Age switch
                    {
                        Age.Minor => today.AddYears(-15),
                        Age.Adult => today.AddYears(-30),
                        _ => null
                    },
                    ProfileType = spec.Profile,
                    TermsAcceptedAt = DateTime.UtcNow,
                    TermsVersion = LegalTerms.CurrentVersion,
                    Language = "it",
                    ReferralCode = ReferralCodes.Generate(),
                    Location = new UserLocation
                    {
                        City = spec.City,
                        Province = spec.Province,
                        Country = "IT",
                        Latitude = (decimal)spec.Lat,
                        Longitude = (decimal)spec.Lon,
                        MaxDistanceKm = 80
                    }
                };
                context.Users.Add(user);
                created[spec.Username] = user;
            }
            if (created.Count > 0) await context.SaveChangesAsync(ct);

            foreach (var spec in missing)
            {
                var user = created[spec.Username];
                foreach (var role in (spec.Roles ?? Array.Empty<string>()).Append("User"))
                    if (roles.TryGetValue(role, out var roleId))
                        context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId, AssignedAt = DateTime.UtcNow });
            }

            // Negozio verificato e punto d'incontro del titolare di prova (partner: profilo Negozio)
            var store = await context.Stores.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Slug == StoreSlug, ct);
            if (store == null)
            {
                var owner = await context.Users.IgnoreQueryFilters().FirstAsync(u => u.Username == "test.negozio", ct);
                store = new Store
                {
                    OwnerId = owner.Id,
                    Name = "Negozio Test",
                    Description = "Negozio di prova per i test manuali: tornei, scambi e ritiri.",
                    Address = "Via Torino 40",
                    City = "Milano",
                    Province = "MI",
                    PostalCode = "20123",
                    Country = "IT",
                    Latitude = 45.4600m,
                    Longitude = 9.1840m,
                    OpeningHours = "Mar–Sab 10:00–19:30",
                    IsSafeMeetupSpot = true,
                    Status = StoreStatus.Verified,
                    ReviewedAt = DateTime.UtcNow,
                    Slug = StoreSlug
                };
                context.Stores.Add(store);
                await context.SaveChangesAsync(ct);
            }

            // Iscritto dal QR del negozio: legato al negozio, lo segue e lo ha come negozio di casa
            if (created.TryGetValue("test.daqr", out var fromQr))
            {
                context.StoreAttributions.Add(new StoreAttribution { UserId = fromQr.Id, StoreId = store.Id, Source = AttributionSource.Qr });
                context.Follows.Add(new Follow { FollowerId = fromQr.Id, FollowedStoreId = store.Id });
                fromQr.HomeStoreId = store.Id;
            }
            // Il collezionista principale ha lo stesso negozio di casa, scelto dalla scheda
            if (created.TryGetValue("test.collezionista", out var collector))
                collector.HomeStoreId = store.Id;

            await context.SaveChangesAsync(ct);
            if (created.Count > 0)
                logger.LogInformation("Utenti di prova creati: {Users} (password {Password}, vedi docs/utenti-di-test.md)",
                    string.Join(", ", created.Keys), Password);
            return created.Count;
        }
    }
}
