using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;

namespace Wms.SecurityAudit;

public sealed class AuditRecord : TenantEntity
{
    public string ActorId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
}

public sealed class SecurityAuditDbContext(DbContextOptions<SecurityAuditDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var audit = modelBuilder.Entity<AuditRecord>();
        audit.ToTable("audit_record", "security_audit", t =>
        {
            t.HasCheckConstraint("ck_audit_version", "version = 1");
        });
        audit.HasKey(x => x.Id);
        audit.HasIndex(x => new { x.TenantId, x.CreatedAt });
        audit.Property(x => x.Metadata).HasColumnType("jsonb");
        audit.HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        Wms.Tenancy.ModelNaming.Apply(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectMutation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectMutation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RejectMutation()
    {
        if (ChangeTracker.Entries<AuditRecord>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit records are append-only.");
    }
}

public interface IAuditWriter
{
    Task RecordAsync(string action, string objectType, string objectId, string result, string? reasonCode, object? metadata, CancellationToken cancellationToken);
}

public sealed class AuditWriter(SecurityAuditDbContext db, ITenantContext tenant) : IAuditWriter
{
    public async Task RecordAsync(string action, string objectType, string objectId, string result, string? reasonCode, object? metadata, CancellationToken cancellationToken)
    {
        if (!tenant.IsResolved) throw new WmsProblemException("TENANT_REQUIRED", "Audit requires a tenant context.", 401);
        db.AuditRecords.Add(new AuditRecord
        {
            TenantId = tenant.TenantId,
            ActorId = tenant.ActorId,
            CreatedBy = tenant.ActorId,
            CorrelationId = tenant.CorrelationId,
            Action = action,
            ObjectType = objectType,
            ObjectId = objectId,
            Result = result,
            ReasonCode = reasonCode,
            Metadata = JsonDocument.Parse(JsonSerializer.Serialize(metadata ?? new { }))
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
