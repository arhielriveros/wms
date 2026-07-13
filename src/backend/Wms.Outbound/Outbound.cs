using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;

namespace Wms.Outbound;

public enum OrderStatus { Imported, Allocated, Released, Picking, Packed, Shipped, Exception }

public sealed class SalesOrderAggregate(OrderStatus status = OrderStatus.Imported, long version = 1)
{
    public OrderStatus Status { get; private set; } = status;
    public long Version { get; private set; } = version;
    public void Allocate(long version) => Transition(OrderStatus.Imported, OrderStatus.Allocated, version);
    public void Release(long version) => Transition(OrderStatus.Allocated, OrderStatus.Released, version);
    public void StartPicking(long version) => Transition(OrderStatus.Released, OrderStatus.Picking, version);
    public void Pack(long version, bool online) { if (!online) throw new WmsProblemException("ONLINE_REQUIRED", "Packing requires connectivity.", 409); Transition(OrderStatus.Picking, OrderStatus.Packed, version); }
    public void Dispatch(long version, bool online) { if (!online) throw new WmsProblemException("ONLINE_REQUIRED", "Dispatch requires connectivity.", 409); Transition(OrderStatus.Packed, OrderStatus.Shipped, version); }
    private void Transition(OrderStatus from, OrderStatus to, long version)
    {
        if (version != Version) throw new WmsProblemException("ORDER_STATE_CONFLICT", "Order version has changed.", 409);
        if (Status != from) throw new WmsProblemException("OUT_INVALID_TRANSITION", "Invalid outbound transition.", 409);
        Status = to;
        Version++;
    }
}

public sealed class SalesOrderDocument : TenantEntity
{
    public Guid SourceMessageId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = string.Empty;
    public string OwnerCode { get; set; } = string.Empty;
    public string? CustomerExternalId { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset RequestedShipAt { get; set; }
    public string Status { get; set; } = nameof(OrderStatus.Imported);
    public List<SalesOrderDocumentLine> Lines { get; set; } = [];
}

public sealed class SalesOrderDocumentLine : TenantEntity
{
    public Guid SalesOrderId { get; set; }
    public string ExternalLineId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal PickedQuantity { get; set; }
    public decimal ShortPickedQuantity { get; set; }
    public string? ShortPickReason { get; set; }
    public string Uom { get; set; } = "EA";
}

public sealed class Shipment : TenantEntity
{
    public Guid SalesOrderId { get; set; }
    public string Status { get; set; } = "Open";
    public DateTimeOffset? DispatchedAt { get; set; }
}

public sealed class OutboundDbContext(DbContextOptions<OutboundDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<SalesOrderDocument> SalesOrders => Set<SalesOrderDocument>();
    public DbSet<SalesOrderDocumentLine> OrderLines => Set<SalesOrderDocumentLine>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SalesOrderDocument>().ToTable("sales_order", "outbound");
        modelBuilder.Entity<SalesOrderDocumentLine>().ToTable("order_line", "outbound");
        modelBuilder.Entity<Shipment>().ToTable("shipment", "outbound");
        modelBuilder.Entity<SalesOrderDocument>().HasIndex(x => new { x.TenantId, x.SourceMessageId }).IsUnique();
        modelBuilder.Entity<SalesOrderDocument>().HasIndex(x => new { x.TenantId, x.WarehouseCode, x.OwnerCode, x.ExternalId }).IsUnique();
        modelBuilder.Entity<SalesOrderDocument>().HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.SalesOrderId);
        modelBuilder.Entity<SalesOrderDocumentLine>().Property(x => x.OrderedQuantity).HasPrecision(18, 4);
        modelBuilder.Entity<SalesOrderDocumentLine>().Property(x => x.AllocatedQuantity).HasPrecision(18, 4);
        modelBuilder.Entity<SalesOrderDocumentLine>().Property(x => x.PickedQuantity).HasPrecision(18, 4);
        modelBuilder.Entity<SalesOrderDocumentLine>().Property(x => x.ShortPickedQuantity).HasPrecision(18, 4);
        modelBuilder.Entity<SalesOrderDocument>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        modelBuilder.Entity<SalesOrderDocumentLine>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        modelBuilder.Entity<Shipment>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        Wms.Tenancy.ModelNaming.Apply(modelBuilder);
    }
}

public interface IOutboundImporter
{
    Task<Guid> ImportAsync(Guid sourceMessageId, Guid tenantId, Guid correlationId, string actor, SalesOrder payload, CancellationToken cancellationToken);
}

public sealed class OutboundImporter(OutboundDbContext db) : IOutboundImporter
{
    public async Task<Guid> ImportAsync(Guid sourceMessageId, Guid tenantId, Guid correlationId, string actor, SalesOrder payload, CancellationToken cancellationToken)
    {
        var duplicate = await db.SalesOrders.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.SourceMessageId == sourceMessageId, cancellationToken);
        if (duplicate is not null) return duplicate.Id;
        if (payload.Lines.Count == 0 || payload.Lines.Any(x => x.Quantity <= 0 || string.IsNullOrWhiteSpace(x.Sku) || string.IsNullOrWhiteSpace(x.Uom)))
            throw new WmsProblemException("VALIDATION_FAILED", "Sales order requires valid positive lines.");
        var order = new SalesOrderDocument
        {
            TenantId = tenantId, CreatedBy = actor, CorrelationId = correlationId, SourceMessageId = sourceMessageId,
            ExternalId = payload.ExternalId, WarehouseCode = payload.WarehouseCode, OwnerCode = payload.OwnerCode,
            CustomerExternalId = payload.CustomerExternalId, Priority = payload.Priority, RequestedShipAt = payload.RequestedShipAt
        };
        foreach (var line in payload.Lines)
            order.Lines.Add(new SalesOrderDocumentLine { TenantId = tenantId, CreatedBy = actor, CorrelationId = correlationId, ExternalLineId = line.ExternalLineId, Sku = line.Sku, OrderedQuantity = line.Quantity, Uom = line.Uom });
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return order.Id;
    }
}
