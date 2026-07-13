using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;

namespace Wms.Integration;

public sealed class InboxMessage : TenantEntity
{
    public Guid MessageId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string PayloadChecksum { get; set; } = string.Empty;
    public JsonDocument Payload { get; set; } = JsonDocument.Parse("{}");
    public string Status { get; set; } = "Accepted";
    public string? ErrorCode { get; set; }
}

public sealed class OutboxMessage : TenantEntity
{
    public Guid MessageId { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = "1.0";
    public string SourceSystem { get; set; } = "WMS";
    public Guid? CausationId { get; set; }
    public JsonDocument Payload { get; set; } = JsonDocument.Parse("{}");
    public string DeliveryKind { get; set; } = "Internal";
    public string? Destination { get; set; }
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastErrorCode { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}

public sealed class DeliveryAttempt : TenantEntity
{
    public Guid OutboxMessageId { get; set; }
    public int AttemptNumber { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? HttpStatus { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}

public sealed class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>().ToTable("inbox", "integration");
        modelBuilder.Entity<OutboxMessage>().ToTable("outbox", "integration");
        modelBuilder.Entity<DeliveryAttempt>().ToTable("delivery_attempt", "integration");
        modelBuilder.Entity<InboxMessage>().HasIndex(x => new { x.TenantId, x.SourceSystem, x.MessageType, x.MessageId }).IsUnique();
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => new { x.TenantId, x.MessageId }).IsUnique();
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => new { x.Status, x.NextAttemptAt });
        modelBuilder.Entity<InboxMessage>().Property(x => x.Payload).HasColumnType("jsonb");
        modelBuilder.Entity<OutboxMessage>().Property(x => x.Payload).HasColumnType("jsonb");
        modelBuilder.Entity<OutboxMessage>().Property(x => x.Version).IsConcurrencyToken();
        modelBuilder.Entity<InboxMessage>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        modelBuilder.Entity<DeliveryAttempt>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        Wms.Tenancy.ModelNaming.Apply(modelBuilder);
    }
}

public sealed record IntegrationMessageDto(Guid MessageId, string Type, string Status, int Attempts, string? ErrorCode, Guid CorrelationId, DateTimeOffset CreatedAt, DateTimeOffset? DeliveredAt);

public interface IIntegrationIngestionService
{
    Task<OperationAccepted> AcceptAsync<T>(CanonicalEnvelope<T> envelope, string expectedType, CancellationToken cancellationToken);
}

public sealed class IntegrationIngestionService(IntegrationDbContext db, ITenantContext tenant) : IIntegrationIngestionService
{
    public async Task<OperationAccepted> AcceptAsync<T>(CanonicalEnvelope<T> envelope, string expectedType, CancellationToken cancellationToken)
    {
        if (!tenant.IsResolved || envelope.TenantId != tenant.TenantId) throw new WmsProblemException("UNAUTHORIZED_SCOPE", "Envelope tenant does not match the authenticated tenant.", 403);
        if (envelope.MessageType != expectedType || envelope.SchemaVersion != "1.0") throw new WmsProblemException("SCHEMA_UNSUPPORTED", "Only canonical schema 1.0 is supported.", 400);
        if (envelope.MessageId == Guid.Empty || envelope.CorrelationId == Guid.Empty || string.IsNullOrWhiteSpace(envelope.SourceSystem))
            throw new WmsProblemException("VALIDATION_FAILED", "Envelope identifiers and source system are required.");
        var payloadJson = JsonSerializer.Serialize(envelope.Payload);
        using var payloadDocument = JsonDocument.Parse(payloadJson);
        var checksum = PayloadChecksum.Compute(payloadDocument.RootElement);
        var existing = await db.Inbox.SingleOrDefaultAsync(x => x.MessageId == envelope.MessageId && x.SourceSystem == envelope.SourceSystem && x.MessageType == envelope.MessageType, cancellationToken);
        if (existing is not null)
        {
            if (existing.PayloadChecksum != checksum) throw new WmsProblemException("DUPLICATE_PAYLOAD_MISMATCH", "Message ID was reused with a different payload.", 409);
            return new OperationAccepted(existing.MessageId, existing.CorrelationId, existing.Status, true);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Inbox.Add(new InboxMessage
        {
            TenantId = tenant.TenantId, CreatedBy = tenant.ActorId, CorrelationId = envelope.CorrelationId,
            MessageId = envelope.MessageId, SourceSystem = envelope.SourceSystem, MessageType = envelope.MessageType,
            SchemaVersion = envelope.SchemaVersion, PayloadChecksum = checksum, Payload = JsonDocument.Parse(payloadJson)
        });
        db.Outbox.Add(new OutboxMessage
        {
            TenantId = tenant.TenantId, CreatedBy = tenant.ActorId, CorrelationId = envelope.CorrelationId,
            MessageId = Guid.NewGuid(), MessageType = $"Import{envelope.MessageType}", CausationId = envelope.MessageId,
            Payload = JsonDocument.Parse(payloadJson), DeliveryKind = "Internal"
        });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new OperationAccepted(envelope.MessageId, envelope.CorrelationId, "Accepted", false);
    }
}

public static class RetrySchedule
{
    private static readonly TimeSpan[] Delays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(6)];
    public static DateTimeOffset Next(int attempt, DateTimeOffset now) => now + Delays[Math.Min(Math.Max(attempt - 1, 0), Delays.Length - 1)] + TimeSpan.FromSeconds(Random.Shared.Next(1, 20));
}
