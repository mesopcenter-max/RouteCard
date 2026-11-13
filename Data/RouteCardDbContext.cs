using HALRouteCardApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HALRouteCardApp.Data;

public sealed class RouteCardDbContext(DbContextOptions<RouteCardDbContext> options) : DbContext(options)
{
    public DbSet<RouteCard> RouteCards => Set<RouteCard>();
    public DbSet<RouteCardLookupSample> RouteCardLookupSamples => Set<RouteCardLookupSample>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UserStamp> UserStamps => Set<UserStamp>();
    public DbSet<RouteCardApproval> RouteCardApprovals => Set<RouteCardApproval>();

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

        var sample = modelBuilder.Entity<RouteCardLookupSample>();
        sample.ToTable("RouteCardLookupSamples");
        sample.HasKey(record => record.Id);
        sample.HasIndex(record => record.WorkOrderNo);
        sample.HasIndex(record => record.PartNo);
        sample.Property(record => record.WorkOrderNo).HasMaxLength(100).IsRequired();
        sample.Property(record => record.PartNo).HasMaxLength(100).IsRequired();
        sample.Property(record => record.JcQty).HasPrecision(18, 3);
        sample.Property(record => record.Project).HasMaxLength(200);
        sample.Property(record => record.ProductType).HasMaxLength(100);
        sample.Property(record => record.OrderNo).HasMaxLength(100).IsRequired();
        sample.Property(record => record.PslNo).HasMaxLength(100);
        sample.Property(record => record.PartName).HasMaxLength(300);
        sample.Property(record => record.HalDrawingNo).HasMaxLength(200);
        sample.Property(record => record.ReferenceDrawingNo).HasColumnType("nvarchar(max)");
        sample.Property(record => record.MaterialSpecification).HasMaxLength(200);
        sample.Property(record => record.MaterialDescription).HasMaxLength(300);
        sample.Property(record => record.MrNo).HasMaxLength(100);
        sample.Property(record => record.MaterialDate).HasColumnType("date");
        sample.Property(record => record.RawMaterialDia).HasMaxLength(100);
        sample.Property(record => record.RawMaterialLength).HasMaxLength(100);
        sample.Property(record => record.RawMaterialBatchNo).HasMaxLength(100);
        sample.Property(record => record.BsWeight).HasMaxLength(100);
        sample.Property(record => record.ForgingWeight).HasMaxLength(100);
        sample.Property(record => record.ForgingPlant).HasMaxLength(100);
        sample.Property(record => record.ForgingBatchNo).HasMaxLength(100);

        var user = modelBuilder.Entity<AppUser>();
        user.ToTable("AppUsers");
        user.HasKey(account => account.Id);
        user.HasIndex(account => account.Username).IsUnique();
        user.Property(account => account.Username).HasMaxLength(200).IsRequired();
        user.Property(account => account.DisplayName).HasMaxLength(100).IsRequired();
        user.Property(account => account.PasswordHash).HasMaxLength(500).IsRequired();
        user.Property(account => account.PhotoUrl).HasMaxLength(500).IsRequired();

        var stamp = modelBuilder.Entity<UserStamp>();
        stamp.ToTable("UserStamps");
        stamp.HasKey(value => value.Id);
        stamp.Property(value => value.StampValue).HasMaxLength(200).IsRequired();
        stamp.Property(value => value.SignatureValue).HasMaxLength(200).IsRequired();
        stamp.HasOne(value => value.AppUser).WithMany(account => account.Stamps).HasForeignKey(value => value.AppUserId);

        var approval = modelBuilder.Entity<RouteCardApproval>();
        approval.ToTable("RouteCardApprovals");
        approval.HasKey(value => value.Id);
        approval.Property(value => value.OperationNo).HasMaxLength(100).IsRequired();
        approval.Property(value => value.StampValue).HasMaxLength(200).IsRequired();
        approval.Property(value => value.SignatureValue).HasMaxLength(200).IsRequired();
        approval.HasOne(value => value.RouteCard).WithMany().HasForeignKey(value => value.RouteCardId);
        approval.HasOne(value => value.AppUser).WithMany().HasForeignKey(value => value.AppUserId);
        approval.HasOne(value => value.UserStamp).WithMany().HasForeignKey(value => value.UserStampId);
    }
}
