using Microsoft.EntityFrameworkCore;
using VoxTrade.Models;

namespace VoxTrade.Api.Data
{
    public class TradingDbContext : DbContext
    {
        public TradingDbContext(DbContextOptions<TradingDbContext> options)
            : base(options)
        {
        }

        // Later you add tables like this:
        // public DbSet<User> Users { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ContactInfo> ContactInfo { get; set; }
        public DbSet<Roles> Roles { get; set; }
        public DbSet<Lookup> LookUp { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<UIThemes> UIThemes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.IsLocked).HasColumnName("is_locked");
                entity.Property(e => e.LockedAt).HasColumnName("locked_at");
                entity.Property(e => e.LockedBy).HasColumnName("locked_by");
                entity.Property(e => e.LockReason).HasColumnName("lock_reason");
            });
        }
    }
}
