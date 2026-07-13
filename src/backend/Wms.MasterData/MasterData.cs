using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;

namespace Wms.MasterData;

public sealed class Owner : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class Sku : TenantEntity
{
    public Guid OwnerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Uom { get; set; } = "EA";
    public string Barcode { get; set; } = string.Empty;
}

public sealed class MasterDataDbContext(DbContextOptions<MasterDataDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Sku> Skus => Set<Sku>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Owner>().ToTable("owner", "master_data");
        modelBuilder.Entity<Sku>().ToTable("sku", "master_data");
        modelBuilder.Entity<Owner>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        modelBuilder.Entity<Sku>().HasIndex(x => new { x.TenantId, x.OwnerId, x.Code }).IsUnique();
        modelBuilder.Entity<Owner>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        modelBuilder.Entity<Sku>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        Wms.Tenancy.ModelNaming.Apply(modelBuilder);
    }
}
