using HALRouteCardApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HALRouteCardApp.Data;

public sealed class RouteCardDbContext(DbContextOptions<RouteCardDbContext> options) : DbContext(options)
{
    public DbSet<RouteCard> RouteCards => Set<RouteCard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RouteCard>();
        entity.ToTable("RouteCards");
        entity.HasKey(card => card.Id);
        entity.Property(card => card.PartNo).HasMaxLength(100).IsRequired();
        entity.HasIndex(card => card.PartNo);
        entity.HasIndex(card => card.OrderNo);
        entity.HasIndex(card => card.WorkOrderNo);
        entity.Property(card => card.Qty).HasPrecision(18, 3);
        entity.Property(card => card.JcQty).HasPrecision(18, 3);
        entity.Property(card => card.RubberQty).HasPrecision(18, 3);
        entity.Property(card => card.ForgingQty).HasPrecision(18, 3);
        entity.Property(card => card.CastingQty).HasPrecision(18, 3);
        entity.Property(card => card.PowderQty).HasPrecision(18, 3);
        entity.Property(card => card.CreatedUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(card => card.UpdatedUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(card => card.TraceabilityJson).HasColumnType("nvarchar(max)");
        entity.Property(card => card.OperationsJson).HasColumnType("nvarchar(max)");
    }
}
