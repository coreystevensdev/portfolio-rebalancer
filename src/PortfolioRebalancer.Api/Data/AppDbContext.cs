using Microsoft.EntityFrameworkCore;
using PortfolioRebalancer.Api.Domain;

namespace PortfolioRebalancer.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<RebalancingEvent> RebalancingEvents => Set<RebalancingEvent>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Portfolio>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.UserId).IsRequired().HasMaxLength(256);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.DriftTolerancePct).HasColumnType("numeric(6,4)");
            e.HasIndex(p => p.UserId);
            e.HasMany(p => p.TargetAllocations).WithOne().HasForeignKey(a => a.PortfolioId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Holdings).WithOne().HasForeignKey(h => h.PortfolioId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.RebalancingEvents).WithOne().HasForeignKey(ev => ev.PortfolioId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<TargetAllocation>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Ticker).IsRequired().HasMaxLength(20);
            e.Property(a => a.TargetWeight).HasColumnType("numeric(8,6)");
        });

        mb.Entity<Holding>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.Ticker).IsRequired().HasMaxLength(20);
            e.HasMany(h => h.Lots).WithOne().HasForeignKey(l => l.HoldingId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(h => h.TotalShares);
        });

        mb.Entity<Lot>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Shares).HasColumnType("numeric(18,6)");
            e.Property(l => l.CostBasisPerShare).HasColumnType("numeric(18,4)");
        });

        mb.Entity<RebalancingEvent>(e =>
        {
            e.HasKey(ev => ev.Id);
            e.HasMany(ev => ev.Orders).WithOne().HasForeignKey(o => o.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<RebalancingOrder>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Ticker).IsRequired().HasMaxLength(20);
            e.Property(o => o.Action).HasConversion<string>();
            e.Property(o => o.Shares).HasColumnType("numeric(18,6)");
            e.Property(o => o.EstimatedValue).HasColumnType("numeric(18,2)");
        });
    }
}
