using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;

namespace Wms.Inbound;

public enum AsnStatus { Imported, Receiving, Received, PutawayInProgress, Completed, Exception }

public sealed class AdvanceShippingNoticeAggregate
{
    public AsnStatus Status { get; private set; } = AsnStatus.Imported;
    public long Version { get; private set; } = 1;

    public void ReleaseReceiving(long expectedVersion) => Transition(AsnStatus.Imported, AsnStatus.Receiving, expectedVersion);
    public void MarkReceived(long expectedVersion) => Transition(AsnStatus.Receiving, AsnStatus.Received, expectedVersion);
    public void ReleasePutaway(long expectedVersion) => Transition(AsnStatus.Received, AsnStatus.PutawayInProgress, expectedVersion);
    public void Complete(long expectedVersion, bool hasOpenTasks, bool quantitiesClosed)
    {
        ValidateVersion(expectedVersion);
        if (Status != AsnStatus.PutawayInProgress || hasOpenTasks || !quantitiesClosed)
            throw new WmsProblemException("INB_INVALID_TRANSITION", "ASN cannot be completed in its current state.", 409);
        Status = AsnStatus.Completed;
        Version++;
    }

    private void Transition(AsnStatus from, AsnStatus to, long expectedVersion)
    {
        ValidateVersion(expectedVersion);
        if (Status != from) throw new WmsProblemException("INB_INVALID_TRANSITION", "Invalid inbound transition.", 409);
        Status = to;
        Version++;
    }

    private void ValidateVersion(long expectedVersion)
    {
        if (expectedVersion != Version) throw new WmsProblemException("ASN_STATE_CONFLICT", "ASN version has changed.", 409);
    }
}

public sealed class Asn : TenantEntity
{
    public Guid SourceMessageId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = string.Empty;
    public string OwnerCode { get; set; } = string.Empty;
    public string? SupplierExternalId { get; set; }
    public DateTimeOffset ExpectedAt { get; set; }
    public string Status { get; set; } = nameof(AsnStatus.Imported);
    public List<AsnLine> Lines { get; set; } = [];
}

public sealed class AsnLine : TenantEntity
{
    public Guid AsnId { get; set; }
    public string ExternalLineId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal ExpectedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal PutawayQuantity { get; set; }
    public string Uom { get; set; } = "EA";
}

public sealed class Receipt : TenantEntity
{
    public Guid AsnId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string Status { get; set; } = "Open";
}

public sealed class InboundDbContext(DbContextOptions<InboundDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<Asn> Asns => Set<Asn>();
    public DbSet<AsnLine> AsnLines => Set<AsnLine>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asn>().ToTable("asn", "inbound");
        modelBuilder.Entity<AsnLine>().ToTable("asn_line", "inbound");
        modelBuilder.Entity<Receipt>().ToTable("receipt", "inbound");
        modelBuilder.Entity<Asn>().HasIndex(x => new { x.TenantId, x.SourceMessageId }).IsUnique();
        modelBuilder.Entity<Asn>().HasIndex(x => new { x.TenantId, x.WarehouseCode, x.OwnerCode, x.ExternalId }).IsUnique();
        modelBuilder.Entity<Asn>().HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.AsnId);
        modelBuilder.Entity<AsnLine>().Property(x => x.ExpectedQuantity).HasPrecision(18, 4);
        modelBuilder.Entity<AsnLine>().Property(x => x.ReceivedQuantity).HasPrecision(18, 4);
        modelBuilder.Entity<AsnLine>().Property(x => x.PutawayQuantity).HasPrecision(18, 4);
        modelBuilder.Entity<Asn>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        modelBuilder.Entity<AsnLine>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        modelBuilder.Entity<Receipt>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        Wms.Tenancy.ModelNaming.Apply(modelBuilder);
    }
}

public interface IInboundImporter
{
    Task<Guid> ImportAsync(Guid sourceMessageId, Guid tenantId, Guid correlationId, string actor, AdvanceShippingNotice payload, CancellationToken cancellationToken);
}

public sealed class InboundImporter(InboundDbContext db) : IInboundImporter
{
    public async Task<Guid> ImportAsync(Guid sourceMessageId, Guid tenantId, Guid correlationId, string actor, AdvanceShippingNotice payload, CancellationToken cancellationToken)
    {
        var duplicate = await db.Asns.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.SourceMessageId == sourceMessageId, cancellationToken);
        if (duplicate is not null) return duplicate.Id;
        if (payload.Lines.Count == 0 || payload.Lines.Any(x => x.Quantity <= 0 || string.IsNullOrWhiteSpace(x.Sku) || string.IsNullOrWhiteSpace(x.Uom)))
            throw new WmsProblemException("VALIDATION_FAILED", "ASN requires valid positive lines.");
        var asn = new Asn
        {
            TenantId = tenantId,
            CreatedBy = actor,
            CorrelationId = correlationId,
            SourceMessageId = sourceMessageId,
            ExternalId = payload.ExternalId,
            WarehouseCode = payload.WarehouseCode,
            OwnerCode = payload.OwnerCode,
            SupplierExternalId = payload.SupplierExternalId,
            ExpectedAt = payload.ExpectedAt
        };
        foreach (var line in payload.Lines)
            asn.Lines.Add(new AsnLine { TenantId = tenantId, CreatedBy = actor, CorrelationId = correlationId, ExternalLineId = line.ExternalLineId, Sku = line.Sku, ExpectedQuantity = line.Quantity, Uom = line.Uom });
        db.Asns.Add(asn);
        await db.SaveChangesAsync(cancellationToken);
        return asn.Id;
    }
}
