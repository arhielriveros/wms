using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;

namespace Wms.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public Guid CorrelationId { get; private set; }
    public string? WarehouseScope { get; private set; }
    public bool IsResolved => TenantId != Guid.Empty;

    public void Resolve(Guid tenantId, string actorId, Guid correlationId, string? warehouseScope)
    {
        if (tenantId == Guid.Empty) throw new WmsProblemException("TENANT_REQUIRED", "A valid tenant is required.", 401);
        if (IsResolved && tenantId != TenantId) throw new WmsProblemException("TENANT_CONTEXT_CONFLICT", "Tenant context cannot change during a request.", 403);
        TenantId = tenantId;
        ActorId = string.IsNullOrWhiteSpace(actorId) ? "unknown" : actorId;
        CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
        WarehouseScope = warehouseScope;
    }
}

public sealed class TenantRecord : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options, ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<TenantRecord> Tenants => Set<TenantRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tenant = modelBuilder.Entity<TenantRecord>();
        tenant.ToTable("tenant", "tenancy");
        tenant.HasKey(x => x.Id);
        tenant.HasIndex(x => x.Code).IsUnique();
        tenant.Property(x => x.Code).HasMaxLength(64);
        tenant.Property(x => x.Name).HasMaxLength(200);
        tenant.HasQueryFilter(x => tenantContext.TenantId == Guid.Empty || x.TenantId == tenantContext.TenantId);
        ModelNaming.Apply(modelBuilder);
    }
}

public static class ModelNaming
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties()) property.SetColumnName(ToSnake(property.Name));
            foreach (var key in entity.GetKeys()) key.SetName(ToSnake(key.GetName() ?? $"pk_{entity.GetTableName()}"));
            foreach (var index in entity.GetIndexes()) index.SetDatabaseName(ToSnake(index.GetDatabaseName() ?? $"ix_{entity.GetTableName()}"));
        }
    }

    private static string ToSnake(string value) => string.Concat(value.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
