using CardExchange.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // DbSets esistenti
        public DbSet<User> Users { get; set; }
        public DbSet<UserLocation> UserLocations { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<CardSet> CardSets { get; set; }
        public DbSet<CardInfo> CardInfos { get; set; }
        public DbSet<Card> Cards { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
      public DbSet<CardPhoto> CardPhotos { get; set; }
        public DbSet<TradeOffer> TradeOffers { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        // Nuovi DbSets - Freemium & Features
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<NotificationPreference> NotificationPreferences { get; set; }
        public DbSet<TradeOfferItem> TradeOfferItems { get; set; }
        public DbSet<TradeReview> TradeReviews { get; set; }
        public DbSet<SavedSearch> SavedSearches { get; set; }

        public DbSet<FavoriteCard> FavoriteCards { get; set; }

        // DbSets - Eventi & Price Tracking
        public DbSet<Event> Events { get; set; }
        public DbSet<EventParticipant> EventParticipants { get; set; }
        public DbSet<PriceHistory> PriceHistories { get; set; }
        public DbSet<PriceAlert> PriceAlerts { get; set; }

        // DbSets - Configurazione
        public DbSet<AppConfig> AppConfigs { get; set; }
        public DbSet<AppConfigKey> AppConfigKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var isSqlite = Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

            // ============================================================
            // User
            // ============================================================
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.Username).IsUnique();
                entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
                entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
                entity.Property(u => u.ReputationScore).HasPrecision(3, 2);
            });

            // ============================================================
            // UserLocation
            // ============================================================
            modelBuilder.Entity<UserLocation>(entity =>
            {
                entity.HasIndex(ul => ul.UserId).IsUnique();

                entity.Property(ul => ul.Latitude)
                      .HasPrecision(10, 8);
                entity.Property(ul => ul.Longitude)
                      .HasPrecision(11, 8);

                entity.HasOne(ul => ul.User)
                      .WithOne(u => u.Location)
                      .HasForeignKey<UserLocation>(ul => ul.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================================
            // Game
            // ============================================================
            modelBuilder.Entity<Game>(entity =>
            {
                entity.HasIndex(g => g.Name).IsUnique();
            });

            // ============================================================
            // CardSet
            // ============================================================
            modelBuilder.Entity<CardSet>(entity =>
            {
                entity.HasIndex(cs => new { cs.GameId, cs.Code }).IsUnique();
                var scryfallIdIdx = entity.HasIndex(cs => cs.ScryfallId).IsUnique();
                if (!isSqlite) scryfallIdIdx.HasFilter("[ScryfallId] IS NOT NULL");
                var pokemonIdIdx = entity.HasIndex(cs => cs.PokemonTcgId).IsUnique();
                if (!isSqlite) pokemonIdIdx.HasFilter("[PokemonTcgId] IS NOT NULL");
                entity.HasOne(cs => cs.Game)
                      .WithMany(g => g.CardSets)
                      .HasForeignKey(cs => cs.GameId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================================
            // CardInfo
            // ============================================================
            modelBuilder.Entity<CardInfo>(entity =>
            {
                entity.HasIndex(ci => new { ci.CardSetId, ci.Name });
                var ciScryfallIdx = entity.HasIndex(ci => ci.ScryfallId).IsUnique();
                if (!isSqlite) ciScryfallIdx.HasFilter("[ScryfallId] IS NOT NULL");
                entity.HasIndex(ci => ci.OracleId);
                var ciPokemonIdx = entity.HasIndex(ci => ci.PokemonTcgId).IsUnique();
                if (!isSqlite) ciPokemonIdx.HasFilter("[PokemonTcgId] IS NOT NULL");
                entity.Property(ci => ci.Cmc).HasPrecision(5, 2);
                entity.Property(ci => ci.PriceUsd).HasPrecision(10, 2);
                entity.Property(ci => ci.PriceUsdFoil).HasPrecision(10, 2);
                entity.Property(ci => ci.PriceEur).HasPrecision(10, 2);
                entity.Property(ci => ci.PriceEurFoil).HasPrecision(10, 2);
                entity.Property(ci => ci.PriceTcgNormal).HasPrecision(10, 2);
                entity.Property(ci => ci.PriceTcgHolofoil).HasPrecision(10, 2);
                entity.Property(ci => ci.PriceTcgReverseHolofoil).HasPrecision(10, 2);
                entity.Property(ci => ci.PriceCardmarketAvg).HasPrecision(10, 2);
                entity.Property(ci => ci.PriceCardmarketTrend).HasPrecision(10, 2);
                entity.HasOne(ci => ci.CardSet)
                      .WithMany(cs => cs.CardInfos)
                      .HasForeignKey(ci => ci.CardSetId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================================
            // Card
            // ============================================================
            modelBuilder.Entity<Card>(entity =>
            {
                entity.HasIndex(c => new { c.UserId, c.CardInfoId });

                entity.Property(c => c.EstimatedValue)
                      .HasPrecision(10, 2);

                entity.HasOne(c => c.User)
                      .WithMany(u => u.Cards)
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(c => c.CardInfo)
                      .WithMany(ci => ci.Cards)
                      .HasForeignKey(c => c.CardInfoId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // CardPhoto
            // ============================================================
            modelBuilder.Entity<CardPhoto>(entity =>
            {
                entity.HasIndex(cp => cp.CardId);
                entity.HasIndex(cp => cp.UploadedByUserId);

                entity.Property(cp => cp.ContentType)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.HasOne(cp => cp.Card)
                      .WithMany(c => c.Photos)
                      .HasForeignKey(cp => cp.CardId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cp => cp.UploadedByUser)
                      .WithMany()
                      .HasForeignKey(cp => cp.UploadedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // WishlistItem
            // ============================================================
            modelBuilder.Entity<WishlistItem>(entity =>
            {
                entity.HasIndex(wi => new { wi.UserId, wi.CardInfoId }).IsUnique();

                entity.Property(wi => wi.MaxPrice)
                      .HasPrecision(10, 2);

                entity.HasOne(wi => wi.User)
                      .WithMany(u => u.WishlistItems)
                      .HasForeignKey(wi => wi.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(wi => wi.CardInfo)
                      .WithMany(ci => ci.WishlistItems)
                      .HasForeignKey(wi => wi.CardInfoId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // TradeOffer
            // ============================================================
            modelBuilder.Entity<TradeOffer>(entity =>
            {
                entity.HasOne(to => to.Sender)
                      .WithMany(u => u.SentOffers)
                      .HasForeignKey(to => to.SenderId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(to => to.Receiver)
                      .WithMany(u => u.ReceivedOffers)
                      .HasForeignKey(to => to.ReceiverId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(to => to.ParentOffer)
                      .WithMany()
                      .HasForeignKey(to => to.ParentOfferId)
                      .OnDelete(DeleteBehavior.Restrict);

                if (!isSqlite)
                    entity.HasCheckConstraint("CK_TradeOffer_DifferentUsers", "[SenderId] != [ReceiverId]");
            });

            // ============================================================
            // TradeOfferItem (NUOVO - link carte a offerte)
            // ============================================================
            modelBuilder.Entity<TradeOfferItem>(entity =>
            {
                entity.HasIndex(toi => new { toi.TradeOfferId, toi.CardId, toi.Side }).IsUnique();

                entity.HasOne(toi => toi.TradeOffer)
                      .WithMany(to => to.Items)
                      .HasForeignKey(toi => toi.TradeOfferId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(toi => toi.Card)
                      .WithMany()
                      .HasForeignKey(toi => toi.CardId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // TradeReview (NUOVO - feedback post-trade)
            // ============================================================
            modelBuilder.Entity<TradeReview>(entity =>
            {
                entity.HasIndex(tr => new { tr.TradeOfferId, tr.ReviewerId }).IsUnique();

                entity.HasOne(tr => tr.TradeOffer)
                      .WithMany(to => to.Reviews)
                      .HasForeignKey(tr => tr.TradeOfferId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(tr => tr.Reviewer)
                      .WithMany()
                      .HasForeignKey(tr => tr.ReviewerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(tr => tr.ReviewedUser)
                      .WithMany()
                      .HasForeignKey(tr => tr.ReviewedUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                if (!isSqlite)
                    entity.HasCheckConstraint("CK_TradeReview_DifferentUsers", "[ReviewerId] != [ReviewedUserId]");
            });

            // ============================================================
            // SubscriptionPlan (NUOVO)
            // ============================================================
            modelBuilder.Entity<SubscriptionPlan>(entity =>
            {
                entity.HasIndex(sp => sp.Name).IsUnique();
                entity.Property(sp => sp.Price).HasPrecision(10, 2);
            });

            // ============================================================
            // UserSubscription (NUOVO)
            // ============================================================
            modelBuilder.Entity<UserSubscription>(entity =>
            {
                entity.HasIndex(us => new { us.UserId, us.Status });
                entity.Property(us => us.AmountPaid).HasPrecision(10, 2);

                entity.HasOne(us => us.User)
                      .WithMany(u => u.Subscriptions)
                      .HasForeignKey(us => us.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(us => us.Plan)
                      .WithMany(sp => sp.Subscriptions)
                      .HasForeignKey(us => us.PlanId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // Conversation (NUOVO - messaggistica)
            // ============================================================
            modelBuilder.Entity<Conversation>(entity =>
            {
                entity.HasIndex(c => new { c.User1Id, c.User2Id }).IsUnique();

                entity.HasOne(c => c.User1)
                      .WithMany()
                      .HasForeignKey(c => c.User1Id)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.User2)
                      .WithMany()
                      .HasForeignKey(c => c.User2Id)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.TradeOffer)
                      .WithMany(to => to.Conversations)
                      .HasForeignKey(c => c.TradeOfferId)
                      .OnDelete(DeleteBehavior.SetNull);

                if (!isSqlite)
                    entity.HasCheckConstraint("CK_Conversation_DifferentUsers", "[User1Id] != [User2Id]");
            });

            // ============================================================
            // Message (NUOVO)
            // ============================================================
            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasIndex(m => new { m.ConversationId, m.CreatedAt });

                entity.HasOne(m => m.Conversation)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(m => m.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.Sender)
                      .WithMany()
                      .HasForeignKey(m => m.SenderId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // Notification (NUOVO)
            // ============================================================
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasIndex(n => new { n.UserId, n.IsRead });
                entity.HasIndex(n => new { n.UserId, n.Type });

                entity.HasOne(n => n.User)
                      .WithMany(u => u.Notifications)
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================================
            // NotificationPreference
            // ============================================================
            modelBuilder.Entity<NotificationPreference>(entity =>
            {
                entity.HasIndex(p => new { p.UserId, p.Type }).IsUnique();

                entity.HasOne(p => p.User)
                      .WithMany(u => u.NotificationPreferences)
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================================
            // SavedSearch (NUOVO - premium)
            // ============================================================
            modelBuilder.Entity<SavedSearch>(entity =>
            {
                entity.HasOne(ss => ss.User)
                      .WithMany(u => u.SavedSearches)
                      .HasForeignKey(ss => ss.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================================
            // Event (Eventi locali)
            // ============================================================
            modelBuilder.Entity<Event>(entity =>
            {
                entity.HasIndex(e => new { e.City, e.StartDate });
                entity.HasIndex(e => new { e.Status, e.StartDate });
                entity.Property(e => e.Latitude).HasPrecision(10, 8);
                entity.Property(e => e.Longitude).HasPrecision(11, 8);

                entity.HasOne(e => e.Organizer)
                      .WithMany()
                      .HasForeignKey(e => e.OrganizerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // EventParticipant
            // ============================================================
            modelBuilder.Entity<EventParticipant>(entity =>
            {
                entity.HasIndex(ep => new { ep.EventId, ep.UserId }).IsUnique();

                entity.HasOne(ep => ep.Event)
                      .WithMany(e => e.Participants)
                      .HasForeignKey(ep => ep.EventId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ep => ep.User)
                      .WithMany()
                      .HasForeignKey(ep => ep.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // PriceHistory (Storico prezzi)
            // ============================================================
            modelBuilder.Entity<PriceHistory>(entity =>
            {
                entity.HasIndex(ph => new { ph.CardInfoId, ph.SnapshotDate }).IsUnique();
                entity.Property(ph => ph.PriceUsd).HasPrecision(10, 2);
                entity.Property(ph => ph.PriceUsdFoil).HasPrecision(10, 2);
                entity.Property(ph => ph.PriceEur).HasPrecision(10, 2);
                entity.Property(ph => ph.PriceEurFoil).HasPrecision(10, 2);

                entity.HasOne(ph => ph.CardInfo)
                      .WithMany()
                      .HasForeignKey(ph => ph.CardInfoId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================================
            // PriceAlert (Alert prezzo)
            // ============================================================
            modelBuilder.Entity<PriceAlert>(entity =>
            {
                entity.HasIndex(pa => new { pa.UserId, pa.CardInfoId });
                entity.HasIndex(pa => new { pa.IsActive, pa.IsTriggered });
                entity.Property(pa => pa.TargetPrice).HasPrecision(10, 2);

                entity.HasOne(pa => pa.User)
                      .WithMany()
                      .HasForeignKey(pa => pa.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pa => pa.CardInfo)
                      .WithMany()
                      .HasForeignKey(pa => pa.CardInfoId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // FavoriteCard (Preferiti carte altrui)
            // ============================================================
            modelBuilder.Entity<FavoriteCard>(entity =>
            {
                entity.HasIndex(fc => new { fc.UserId, fc.CardId }).IsUnique();

                entity.HasOne(fc => fc.User)
                      .WithMany(u => u.FavoriteCards)
                      .HasForeignKey(fc => fc.UserId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(fc => fc.Card)
                      .WithMany()
                      .HasForeignKey(fc => fc.CardId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // ============================================================
            // RBAC
            // ============================================================
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasIndex(r => r.Name).IsUnique();
                entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
                entity.HasQueryFilter(r => !r.IsDeleted);
                entity.Navigation(r => r.RolePermissions).AutoInclude(false);
            });

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasIndex(p => p.Name).IsUnique();
                entity.Property(p => p.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(p => p.Category);
                entity.HasQueryFilter(p => !p.IsDeleted);
                entity.Navigation(p => p.RolePermissions).AutoInclude(false);
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();

                entity.HasOne(ur => ur.User)
                      .WithMany(u => u.UserRoles)
                      .HasForeignKey(ur => ur.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ur => ur.Role)
                      .WithMany(r => r.UserRoles)
                      .HasForeignKey(ur => ur.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ur => ur.AssignedByUser)
                      .WithMany()
                      .HasForeignKey(ur => ur.AssignedBy)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

                entity.HasOne(rp => rp.Role)
                      .WithMany(r => r.RolePermissions)
                      .HasForeignKey(rp => rp.RoleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rp => rp.Permission)
                      .WithMany(p => p.RolePermissions)
                      .HasForeignKey(rp => rp.PermissionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(rp => !rp.Role.IsDeleted && !rp.Permission.IsDeleted);
            });

            // ============================================================
            // AppConfig (Configurazioni di sistema)
            // ============================================================
            modelBuilder.Entity<AppConfig>(entity =>
            {
                entity.HasIndex(ac => ac.Key).IsUnique();
                entity.HasIndex(ac => ac.Category);
            });

            // ============================================================
            // AppConfigKey (Chiavi API e segreti)
            // ============================================================
            modelBuilder.Entity<AppConfigKey>(entity =>
            {
                entity.HasIndex(ak => new { ak.ServiceName, ak.KeyName }).IsUnique();
            });

            // ============================================================
            // Query filters per soft delete
            // ============================================================
            modelBuilder.Entity<User>().HasQueryFilter(u => u.IsActive);
            modelBuilder.Entity<UserLocation>().HasQueryFilter(ul => !ul.IsDeleted);
            modelBuilder.Entity<Game>().HasQueryFilter(g => !g.IsDeleted);
            modelBuilder.Entity<CardSet>().HasQueryFilter(cs => !cs.IsDeleted);
            modelBuilder.Entity<CardInfo>().HasQueryFilter(ci => !ci.IsDeleted);
            modelBuilder.Entity<Card>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<WishlistItem>().HasQueryFilter(wi => !wi.IsDeleted);
            modelBuilder.Entity<TradeOffer>().HasQueryFilter(to => !to.IsDeleted);
            modelBuilder.Entity<UserRole>().HasQueryFilter(ur => !ur.IsDeleted);
            modelBuilder.Entity<TradeOfferItem>().HasQueryFilter(toi => !toi.IsDeleted);
            modelBuilder.Entity<TradeReview>().HasQueryFilter(tr => !tr.IsDeleted);
            modelBuilder.Entity<SubscriptionPlan>().HasQueryFilter(sp => !sp.IsDeleted);
            modelBuilder.Entity<UserSubscription>().HasQueryFilter(us => !us.IsDeleted);
            modelBuilder.Entity<Conversation>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Message>().HasQueryFilter(m => !m.IsDeleted);
            modelBuilder.Entity<Notification>().HasQueryFilter(n => !n.IsDeleted);
            modelBuilder.Entity<SavedSearch>().HasQueryFilter(ss => !ss.IsDeleted);
            modelBuilder.Entity<Event>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<EventParticipant>().HasQueryFilter(ep => !ep.IsDeleted);
            modelBuilder.Entity<PriceHistory>().HasQueryFilter(ph => !ph.IsDeleted);
            modelBuilder.Entity<PriceAlert>().HasQueryFilter(pa => !pa.IsDeleted);
            modelBuilder.Entity<FavoriteCard>().HasQueryFilter(fc => !fc.IsDeleted);
            modelBuilder.Entity<AppConfig>().HasQueryFilter(ac => !ac.IsDeleted);
            modelBuilder.Entity<AppConfigKey>().HasQueryFilter(ak => !ak.IsDeleted);
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var now = DateTime.UtcNow;

            var baseEntries = ChangeTracker.Entries<BaseEntity>();
            foreach (var entry in baseEntries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = now;
                        entry.Entity.UpdatedAt = now;
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = now;
                        break;
                }
            }

            var userEntries = ChangeTracker.Entries<User>();
            foreach (var entry in userEntries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = now;
                        entry.Entity.UpdatedAt = now;
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = now;
                        break;
                }
            }
        }
    }
}
