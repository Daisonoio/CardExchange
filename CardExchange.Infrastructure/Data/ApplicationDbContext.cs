using CardExchange.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // DbSets
        public DbSet<User> Users { get; set; }
        public DbSet<UserLocation> UserLocations { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<CardSet> CardSets { get; set; }
        public DbSet<CardInfo> CardInfos { get; set; }
        public DbSet<Card> Cards { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<TradeOffer> TradeOffers { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurazioni User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.Username).IsUnique();
                entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
                entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
            });

            // Configurazioni UserLocation
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

            // Configurazioni Game
            modelBuilder.Entity<Game>(entity =>
            {
                entity.HasIndex(g => g.Name).IsUnique();
            });

            // Configurazioni CardSet
            modelBuilder.Entity<CardSet>(entity =>
            {
                entity.HasIndex(cs => new { cs.GameId, cs.Code }).IsUnique();
                entity.HasOne(cs => cs.Game)
                      .WithMany(g => g.CardSets)
                      .HasForeignKey(cs => cs.GameId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configurazioni CardInfo
            modelBuilder.Entity<CardInfo>(entity =>
            {
                entity.HasIndex(ci => new { ci.CardSetId, ci.Name });
                entity.HasOne(ci => ci.CardSet)
                      .WithMany(cs => cs.CardInfos)
                      .HasForeignKey(ci => ci.CardSetId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configurazioni Card
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

            // Configurazioni WishlistItem
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

            // Configurazioni TradeOffer - CORRETTE
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

                entity.HasCheckConstraint("CK_TradeOffer_DifferentUsers", "[SenderId] != [ReceiverId]");
            });

            // Configurazione Role
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasIndex(r => r.Name).IsUnique();
                entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
            });

            // Configurazione Permission
            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasIndex(p => p.Name).IsUnique();
                entity.Property(p => p.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(p => p.Category);
            });

            // Configurazione UserRole
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

            // Configurazione RolePermission
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

            // Query filters per soft delete
            modelBuilder.Entity<UserLocation>().HasQueryFilter(ul => !ul.IsDeleted);
            modelBuilder.Entity<Game>().HasQueryFilter(g => !g.IsDeleted);
            modelBuilder.Entity<CardSet>().HasQueryFilter(cs => !cs.IsDeleted);
            modelBuilder.Entity<CardInfo>().HasQueryFilter(ci => !ci.IsDeleted);
            modelBuilder.Entity<Card>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<WishlistItem>().HasQueryFilter(wi => !wi.IsDeleted);
            modelBuilder.Entity<TradeOffer>().HasQueryFilter(to => !to.IsDeleted);
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasQueryFilter(r => !r.IsDeleted);
                entity.Navigation(r => r.RolePermissions).AutoInclude(false);
            });

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasQueryFilter(p => !p.IsDeleted);
                entity.Navigation(p => p.RolePermissions).AutoInclude(false);
            });
            modelBuilder.Entity<UserRole>().HasQueryFilter(ur => !ur.IsDeleted);
            // Per User usiamo IsActive invece di IsDeleted
            modelBuilder.Entity<User>().HasQueryFilter(u => u.IsActive);
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
            var baseEntries = ChangeTracker.Entries<BaseEntity>();
            foreach (var entry in baseEntries)
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
            }

            var userEntries = ChangeTracker.Entries<User>();
            foreach (var entry in userEntries)
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}