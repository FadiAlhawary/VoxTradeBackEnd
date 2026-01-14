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
        public DbSet<Roles> Roles { get; set; }
        public DbSet<UIThemes> UIThemes { get; set; }
    }
}
