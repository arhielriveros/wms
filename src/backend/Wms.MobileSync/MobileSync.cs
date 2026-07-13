using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;
using Wms.TaskExecution;

namespace Wms.MobileSync;

public sealed class RegisteredDevice : TenantEntity
{
    public string DeviceId { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset LastSeenAt { get; set; }
}

public sealed class CommandInbox : TenantEntity
{
    public Guid CommandId { get; set; }
    public Guid TaskId { get; set; }
    public long LocalSequence { get; set; }
    public string PayloadChecksum { get; set; } = string.Empty;
    public string ResultStatus { get; set; } = string.Empty;
    public string ResultCode { get; set; } = string.Empty;
    public string ResultMessage { get; set; } = string.Empty;
    public long? CurrentVersion { get; set; }
    public string SuggestedAction { get; set; } = string.Empty;
}

public sealed class SyncCheckpoint : TenantEntity
{
    public string DeviceId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public long Value { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class MobileSyncDbContext(DbContextOptions<MobileSyncDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<RegisteredDevice> Devices => Set<RegisteredDevice>();
    public DbSet<CommandInbox> CommandInbox => Set<CommandInbox>();
    public DbSet<SyncCheckpoint> Checkpoints => Set<SyncCheckpoint>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegisteredDevice>().ToTable("device", "mobile_sync");
        modelBuilder.Entity<CommandInbox>().ToTable("command_inbox", "mobile_sync");
        modelBuilder.Entity<SyncCheckpoint>().ToTable("sync_checkpoint", "mobile_sync");
        modelBuilder.Entity<RegisteredDevice>().HasIndex(x => new { x.TenantId, x.DeviceId }).IsUnique();
        modelBuilder.Entity<CommandInbox>().HasIndex(x => new { x.TenantId, x.CommandId }).IsUnique();
        modelBuilder.Entity<SyncCheckpoint>().HasIndex(x => new { x.TenantId, x.DeviceId, x.UserId }).IsUnique();
        modelBuilder.Entity<RegisteredDevice>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        modelBuilder.Entity<CommandInbox>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        modelBuilder.Entity<SyncCheckpoint>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        Wms.Tenancy.ModelNaming.Apply(modelBuilder);
    }
}

public interface IMobileCommandProcessor
{
    Task<IReadOnlyList<MobileCommandResult>> ProcessBatchAsync(MobileCommandBatch batch, CancellationToken cancellationToken);
}

public sealed class MobileCommandProcessor(MobileSyncDbContext db, ITenantContext tenant, IOfflineTaskExecutor executor) : IMobileCommandProcessor
{
    public async Task<IReadOnlyList<MobileCommandResult>> ProcessBatchAsync(MobileCommandBatch batch, CancellationToken cancellationToken)
    {
        if (batch.Commands.Count is < 1 or > 100) throw new WmsProblemException("VALIDATION_FAILED", "Batch must contain 1 to 100 commands.");
        var commandIds = batch.Commands.Select(x => x.CommandId).Distinct().ToList();
        var priorById = await db.CommandInbox.Where(x => commandIds.Contains(x.CommandId)).ToDictionaryAsync(x => x.CommandId, cancellationToken);
        await executor.PrepareAsync(batch.Commands.Select(x => x.TaskId).Distinct().ToArray(), cancellationToken);
        var results = new Dictionary<Guid, MobileCommandResult>();
        foreach (var group in batch.Commands.GroupBy(x => x.TaskId))
        {
            var blocked = false;
            foreach (var command in group.OrderBy(x => x.LocalSequence))
            {
                MobileCommandResult result;
                if (blocked)
                {
                    result = new(command.CommandId, MobileCommandStatus.RequiresReview, "PREVIOUS_COMMAND_FAILED", "A previous command for this task failed.", null, "Resolve the first failed command.");
                }
                else
                {
                    result = await ProcessOneAsync(command, priorById, cancellationToken);
                    blocked = result.Status is not (MobileCommandStatus.Accepted or MobileCommandStatus.AlreadyProcessed);
                }
                results[command.CommandId] = result;
            }
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);
        return batch.Commands.Select(x => results[x.CommandId]).ToArray();
    }

    private async Task<MobileCommandResult> ProcessOneAsync(OfflineCommand command, IDictionary<Guid, CommandInbox> priorById, CancellationToken cancellationToken)
    {
        if (!tenant.IsResolved || command.TenantId != tenant.TenantId)
            return new(command.CommandId, MobileCommandStatus.Unauthorized, "UNAUTHORIZED_SCOPE", "Tenant scope is invalid.", null, "Reauthenticate.");
        if (command.SchemaVersion != "1.0" || command.LocalSequence < 0)
            return new(command.CommandId, MobileCommandStatus.Rejected, "SCHEMA_UNSUPPORTED", "Command schema is invalid.", null, "Update the mobile client.");
        var checksum = PayloadChecksum.Compute(command);
        if (priorById.TryGetValue(command.CommandId, out var prior))
        {
            if (prior.PayloadChecksum != checksum)
                return new(command.CommandId, MobileCommandStatus.Rejected, "DUPLICATE_PAYLOAD_MISMATCH", "Command ID was reused with another payload.", prior.CurrentVersion, "Create a new command ID.");
            return new(command.CommandId, MobileCommandStatus.AlreadyProcessed, prior.ResultCode, prior.ResultMessage, prior.CurrentVersion, prior.SuggestedAction);
        }
        var result = await executor.ExecuteAsync(command, cancellationToken);
        var persistedByOwner = db.CommandInbox.Local.FirstOrDefault(x => x.CommandId == command.CommandId);
        if (persistedByOwner is not null)
        {
            priorById[command.CommandId] = persistedByOwner;
            return result;
        }
        var inbox = new CommandInbox
        {
            TenantId = tenant.TenantId, CreatedBy = tenant.ActorId, CorrelationId = tenant.CorrelationId,
            CommandId = command.CommandId, TaskId = command.TaskId, LocalSequence = command.LocalSequence,
            PayloadChecksum = checksum, ResultStatus = result.Status.ToString(), ResultCode = result.Code,
            ResultMessage = result.Message, CurrentVersion = result.CurrentVersion, SuggestedAction = result.SuggestedAction
        };
        db.CommandInbox.Add(inbox);
        priorById[command.CommandId] = inbox;
        return result;
    }
}
