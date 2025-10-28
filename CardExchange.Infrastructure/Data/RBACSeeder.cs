using CardExchange.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Data
{
    public static class RBACSeeder
    {
        public static async Task SeedRolesAndPermissions(ApplicationDbContext context)
        {
            // Verifica se i dati esistono già
            if (await context.Roles.AnyAsync())
            {
                return; // Dati già presenti
            }

            // === CREA RUOLI ===
            var roles = new List<Role>
            {
                new Role { Name = "SuperAdmin", Description = "Amministratore di sistema con accesso completo", IsSystemRole = true },
                new Role { Name = "Admin", Description = "Amministratore con privilegi elevati", IsSystemRole = true },
                new Role { Name = "Moderator", Description = "Moderatore della community", IsSystemRole = true },
                new Role { Name = "PremiumUser", Description = "Utente con funzionalità premium", IsSystemRole = true },
                new Role { Name = "User", Description = "Utente standard verificato", IsSystemRole = true },
                new Role { Name = "Guest", Description = "Utente non verificato", IsSystemRole = true }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();

            // === CREA PERMESSI ===
            var permissions = new List<Permission>
            {
                // USERS
                new Permission { Name = "USERS.READ.ALL", Category = "Users", Description = "Visualizzare tutti gli utenti" },
                new Permission { Name = "USERS.READ.PUBLIC", Category = "Users", Description = "Visualizzare profili pubblici" },
                new Permission { Name = "USERS.READ.OWN", Category = "Users", Description = "Visualizzare proprio profilo" },
                new Permission { Name = "USERS.UPDATE.OWN", Category = "Users", Description = "Modificare proprio profilo" },
                new Permission { Name = "USERS.UPDATE.ANY", Category = "Users", Description = "Modificare qualsiasi utente" },
                new Permission { Name = "USERS.DELETE.ANY", Category = "Users", Description = "Eliminare qualsiasi utente" },
                new Permission { Name = "USERS.BAN", Category = "Users", Description = "Bannare utenti" },
                new Permission { Name = "USERS.ASSIGN_ROLES", Category = "Users", Description = "Assegnare ruoli" },

                // CARDS
                new Permission { Name = "CARDS.READ.ALL", Category = "Cards", Description = "Visualizzare tutte le carte" },
                new Permission { Name = "CARDS.CREATE.OWN", Category = "Cards", Description = "Creare proprie carte" },
                new Permission { Name = "CARDS.UPDATE.OWN", Category = "Cards", Description = "Modificare proprie carte" },
                new Permission { Name = "CARDS.DELETE.OWN", Category = "Cards", Description = "Eliminare proprie carte" },
                new Permission { Name = "CARDS.DELETE.ANY", Category = "Cards", Description = "Eliminare qualsiasi carta" },
                new Permission { Name = "CARDS.LIMIT.UNLIMITED", Category = "Cards", Description = "Nessun limite carte" },
                new Permission { Name = "CARDS.EXPORT", Category = "Cards", Description = "Esportare collezione" },

                // WISHLIST
                new Permission { Name = "WISHLIST.READ.OWN", Category = "Wishlist", Description = "Visualizzare propria wishlist" },
                new Permission { Name = "WISHLIST.CREATE.OWN", Category = "Wishlist", Description = "Creare wishlist" },
                new Permission { Name = "WISHLIST.UPDATE.OWN", Category = "Wishlist", Description = "Modificare wishlist" },
                new Permission { Name = "WISHLIST.DELETE.OWN", Category = "Wishlist", Description = "Eliminare wishlist" },
                new Permission { Name = "WISHLIST.LIMIT.UNLIMITED", Category = "Wishlist", Description = "Nessun limite wishlist" },

                // CATALOG
                new Permission { Name = "CATALOG.READ.ALL", Category = "Catalog", Description = "Visualizzare catalogo" },
                new Permission { Name = "CATALOG.CREATE", Category = "Catalog", Description = "Creare elementi catalogo" },
                new Permission { Name = "CATALOG.UPDATE", Category = "Catalog", Description = "Modificare catalogo" },
                new Permission { Name = "CATALOG.DELETE", Category = "Catalog", Description = "Eliminare elementi catalogo" },

                // TRADES
                new Permission { Name = "TRADES.READ.OWN", Category = "Trades", Description = "Visualizzare proprie offerte" },
                new Permission { Name = "TRADES.READ.ALL", Category = "Trades", Description = "Visualizzare tutte le offerte" },
                new Permission { Name = "TRADES.CREATE", Category = "Trades", Description = "Creare offerte" },
                new Permission { Name = "TRADES.RESPOND", Category = "Trades", Description = "Rispondere a offerte" },
                new Permission { Name = "TRADES.CANCEL.OWN", Category = "Trades", Description = "Cancellare proprie offerte" },
                new Permission { Name = "TRADES.CANCEL.ANY", Category = "Trades", Description = "Cancellare qualsiasi offerta" },

                // SEARCH
                new Permission { Name = "SEARCH.BASIC", Category = "Search", Description = "Ricerca base" },
                new Permission { Name = "SEARCH.ADVANCED", Category = "Search", Description = "Ricerca avanzata" },
                new Permission { Name = "SEARCH.GEOGRAPHIC", Category = "Search", Description = "Ricerca geografica" },
                new Permission { Name = "SEARCH.SAVE", Category = "Search", Description = "Salvare ricerche" },

                // STATS
                new Permission { Name = "STATS.VIEW.OWN", Category = "Stats", Description = "Visualizzare proprie statistiche" },
                new Permission { Name = "STATS.VIEW.ADVANCED", Category = "Stats", Description = "Statistiche avanzate" },
                new Permission { Name = "STATS.VIEW.GLOBAL", Category = "Stats", Description = "Statistiche globali" },
                new Permission { Name = "STATS.EXPORT", Category = "Stats", Description = "Esportare statistiche" },

                // ADMIN
                new Permission { Name = "ADMIN.DASHBOARD", Category = "Admin", Description = "Accedere a dashboard admin" },
                new Permission { Name = "ADMIN.LOGS.VIEW", Category = "Admin", Description = "Visualizzare log" },
                new Permission { Name = "ADMIN.REPORTS.MANAGE", Category = "Admin", Description = "Gestire segnalazioni" },
                new Permission { Name = "ADMIN.SYSTEM.CONFIG", Category = "Admin", Description = "Configurare sistema" }
            };

            await context.Permissions.AddRangeAsync(permissions);
            await context.SaveChangesAsync();

            // === ASSEGNA PERMESSI AI RUOLI ===

            // Ottieni i ruoli e permessi dal database
            var superAdmin = await context.Roles.FirstAsync(r => r.Name == "SuperAdmin");
            var admin = await context.Roles.FirstAsync(r => r.Name == "Admin");
            var moderator = await context.Roles.FirstAsync(r => r.Name == "Moderator");
            var premiumUser = await context.Roles.FirstAsync(r => r.Name == "PremiumUser");
            var user = await context.Roles.FirstAsync(r => r.Name == "User");
            var guest = await context.Roles.FirstAsync(r => r.Name == "Guest");

            var allPermissions = await context.Permissions.ToListAsync();

            // SuperAdmin - TUTTI i permessi
            foreach (var permission in allPermissions)
            {
                await context.RolePermissions.AddAsync(new RolePermission
                {
                    RoleId = superAdmin.Id,
                    PermissionId = permission.Id
                });
            }

            // Admin - Quasi tutti tranne USERS.ASSIGN_ROLES e ADMIN.SYSTEM.CONFIG
            var adminPermissions = allPermissions
                .Where(p => p.Name != "USERS.ASSIGN_ROLES" && p.Name != "ADMIN.SYSTEM.CONFIG")
                .ToList();

            foreach (var permission in adminPermissions)
            {
                await context.RolePermissions.AddAsync(new RolePermission
                {
                    RoleId = admin.Id,
                    PermissionId = permission.Id
                });
            }

            // Moderator
            var moderatorPermissionNames = new[]
            {
                "USERS.READ.ALL", "USERS.READ.PUBLIC", "USERS.READ.OWN", "USERS.UPDATE.OWN",
                "CARDS.READ.ALL", "CARDS.CREATE.OWN", "CARDS.UPDATE.OWN", "CARDS.DELETE.OWN", "CARDS.LIMIT.UNLIMITED", "CARDS.EXPORT",
                "WISHLIST.READ.OWN", "WISHLIST.CREATE.OWN", "WISHLIST.UPDATE.OWN", "WISHLIST.DELETE.OWN", "WISHLIST.LIMIT.UNLIMITED",
                "CATALOG.READ.ALL",
                "TRADES.READ.OWN", "TRADES.READ.ALL", "TRADES.CREATE", "TRADES.RESPOND", "TRADES.CANCEL.OWN",
                "SEARCH.BASIC", "SEARCH.ADVANCED", "SEARCH.GEOGRAPHIC", "SEARCH.SAVE",
                "STATS.VIEW.OWN", "STATS.VIEW.ADVANCED", "STATS.VIEW.GLOBAL", "STATS.EXPORT",
                "ADMIN.DASHBOARD", "ADMIN.LOGS.VIEW", "ADMIN.REPORTS.MANAGE"
            };

            foreach (var permName in moderatorPermissionNames)
            {
                var perm = allPermissions.First(p => p.Name == permName);
                await context.RolePermissions.AddAsync(new RolePermission
                {
                    RoleId = moderator.Id,
                    PermissionId = perm.Id
                });
            }

            // PremiumUser
            var premiumPermissionNames = new[]
            {
                "USERS.READ.PUBLIC", "USERS.READ.OWN", "USERS.UPDATE.OWN",
                "CARDS.READ.ALL", "CARDS.CREATE.OWN", "CARDS.UPDATE.OWN", "CARDS.DELETE.OWN", "CARDS.LIMIT.UNLIMITED", "CARDS.EXPORT",
                "WISHLIST.READ.OWN", "WISHLIST.CREATE.OWN", "WISHLIST.UPDATE.OWN", "WISHLIST.DELETE.OWN", "WISHLIST.LIMIT.UNLIMITED",
                "CATALOG.READ.ALL",
                "TRADES.READ.OWN", "TRADES.CREATE", "TRADES.RESPOND", "TRADES.CANCEL.OWN",
                "SEARCH.BASIC", "SEARCH.ADVANCED", "SEARCH.GEOGRAPHIC", "SEARCH.SAVE",
                "STATS.VIEW.OWN", "STATS.VIEW.ADVANCED", "STATS.EXPORT"
            };

            foreach (var permName in premiumPermissionNames)
            {
                var perm = allPermissions.First(p => p.Name == permName);
                await context.RolePermissions.AddAsync(new RolePermission
                {
                    RoleId = premiumUser.Id,
                    PermissionId = perm.Id
                });
            }

            // User
            var userPermissionNames = new[]
            {
                "USERS.READ.PUBLIC", "USERS.READ.OWN", "USERS.UPDATE.OWN",
                "CARDS.READ.ALL", "CARDS.CREATE.OWN", "CARDS.UPDATE.OWN", "CARDS.DELETE.OWN",
                "WISHLIST.READ.OWN", "WISHLIST.CREATE.OWN", "WISHLIST.UPDATE.OWN", "WISHLIST.DELETE.OWN",
                "CATALOG.READ.ALL",
                "TRADES.READ.OWN", "TRADES.CREATE", "TRADES.RESPOND", "TRADES.CANCEL.OWN",
                "SEARCH.BASIC", "SEARCH.ADVANCED", "SEARCH.GEOGRAPHIC",
                "STATS.VIEW.OWN"
            };

            foreach (var permName in userPermissionNames)
            {
                var perm = allPermissions.First(p => p.Name == permName);
                await context.RolePermissions.AddAsync(new RolePermission
                {
                    RoleId = user.Id,
                    PermissionId = perm.Id
                });
            }

            // Guest
            var guestPermissionNames = new[]
            {
                "USERS.READ.PUBLIC", "USERS.READ.OWN",
                "CARDS.READ.ALL",
                "CATALOG.READ.ALL",
                "SEARCH.BASIC"
            };

            foreach (var permName in guestPermissionNames)
            {
                var perm = allPermissions.First(p => p.Name == permName);
                await context.RolePermissions.AddAsync(new RolePermission
                {
                    RoleId = guest.Id,
                    PermissionId = perm.Id
                });
            }

            await context.SaveChangesAsync();
        }
    }
}