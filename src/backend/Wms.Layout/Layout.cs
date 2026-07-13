using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;

namespace Wms.Layout;

public sealed class Warehouse : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class Location : TenantEntity
{
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string ZoneCode { get; set; } = string.Empty;
    public string Type { get; set; } = "Storage";
    public bool IsActive { get; set; } = true;
}

public sealed class LayoutDbContext(DbContextOptions<LayoutDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Location> Locations => Set<Location>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Warehouse>().ToTable("warehouse", "layout");
        modelBuilder.Entity<Location>().ToTable("location", "layout");
        modelBuilder.Entity<Warehouse>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        modelBuilder.Entity<Location>().HasIndex(x => new { x.TenantId, x.WarehouseId, x.Code }).IsUnique();
        modelBuilder.Entity<Warehouse>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        modelBuilder.Entity<Location>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        Wms.Tenancy.ModelNaming.Apply(modelBuilder);
    }
}
