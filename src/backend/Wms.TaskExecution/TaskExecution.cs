using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;

namespace Wms.TaskExecution;

public enum WarehouseTaskStatus { Created, Assigned, InProgress, Completed, Cancelled, Exception }

public sealed class WarehouseTaskAggregate(WarehouseTaskStatus status = WarehouseTaskStatus.Created, long version = 1)
{
    public WarehouseTaskStatus Status { get; private set; } = status;
    public long Version { get; private set; } = version;
    public void Assign(long expectedVersion) => Transition(WarehouseTaskStatus.Created, WarehouseTaskStatus.Assigned, expectedVersion);
    public void Start(long expectedVersion, DateTimeOffset? expiresAt, DateTimeOffset now)
    {
        if (expiresAt <= now) throw new WmsProblemException("TASK_EXPIRED", "Task assignment has expired.", 409);
        Transition(WarehouseTaskStatus.Assigned, WarehouseTaskStatus.InProgress, expectedVersion);
    }
    public void Complete(long expectedVersion, bool ownerEffectConfirmed)
    {
        if (!ownerEffectConfirmed) throw new WmsProblemException("OWNER_EFFECT_NOT_CONFIRMED", "The owning module has not confirmed the physical effect.", 409);
        Transition(WarehouseTaskStatus.InProgress, WarehouseTaskStatus.Completed, expectedVersion);
    }
    public void Exception(long expectedVersion)
    {
        if (expectedVersion != Version) throw new WmsProblemException("TASK_VERSION_CONFLICT", "Task version has changed.", 409);
        if (Status is WarehouseTaskStatus.Completed or WarehouseTaskStatus.Cancelled) throw new WmsProblemException("TSK_INVALID_TRANSITION", "Terminal task cannot enter exception.", 409);
        Status = WarehouseTaskStatus.Exception; Version++;
    }
    private void Transition(WarehouseTaskStatus from, WarehouseTaskStatus to, long expectedVersion)
    {
        if (expectedVersion != Version) throw new WmsProblemException("TASK_VERSION_CONFLICT", "Task version has changed.", 409);
        if (Status != from) throw new WmsProblemException("TSK_INVALID_TRANSITION", "Invalid task transition.", 409);
        Status = to; Version++;
    }
}

public sealed class WarehouseTask : TenantEntity
{
    public Guid WarehouseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public Guid OwnerEntityId { get; set; }
    public string? AssigneeId { get; set; }
    public string? DeviceId { get; set; }
    public string? ZoneCode { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string Status { get; set; } = nameof(WarehouseTaskStatus.Created);
    public int Priority { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<WarehouseTaskStep> Steps { get; set; } = [];
}

public sealed class WarehouseTaskStep : TenantEntity
{
    public Guid TaskId { get; set; }
    public int Sequence { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? LocationBarcode { get; set; }
    public string? SkuBarcode { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "EA";
    public bool Completed { get; set; }
}

public sealed class TaskExecutionDbContext(DbContextOptions<TaskExecutionDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<WarehouseTask> Tasks => Set<WarehouseTask>();
    public DbSet<WarehouseTaskStep> Steps => Set<WarehouseTaskStep>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WarehouseTask>().ToTable("task", "task_execution");
        modelBuilder.Entity<WarehouseTaskStep>().ToTable("task_step", "task_execution");
        modelBuilder.Entity<WarehouseTask>().HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.TaskId);
        modelBuilder.Entity<WarehouseTask>().HasIndex(x => new { x.TenantId, x.WarehouseId, x.Status, x.AssigneeId });
        modelBuilder.Entity<WarehouseTask>().Property(x => x.Version).IsConcurrencyToken();
        modelBuilder.Entity<WarehouseTask>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        modelBuilder.Entity<WarehouseTaskStep>().HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        Wms.Tenancy.ModelNaming.Apply(modelBuilder);
    }
}

public interface IOfflineTaskExecutor
{
    Task PrepareAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken);
    Task<MobileCommandResult> ExecuteAsync(OfflineCommand command, CancellationToken cancellationToken);
}

public interface IOfflineTaskCommandHandler
{
    bool CanHandle(WarehouseTask task, OfflineCommand command);
    Task<MobileCommandResult> ExecuteAsync(WarehouseTask task, OfflineCommand command, CancellationToken cancellationToken);
}

public sealed class OfflineTaskExecutor(TaskExecutionDbContext db, IEnumerable<IOfflineTaskCommandHandler> handlers) : IOfflineTaskExecutor
{
    private Dictionary<Guid, WarehouseTask?> preparedTasks = [];

    public async Task PrepareAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken)
    {
        var ids = taskIds.Distinct().ToList();
        var loaded = await db.Tasks.Include(x => x.Steps).Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        preparedTasks = ids.ToDictionary(id => id, id => loaded.GetValueOrDefault(id));
    }

    public async Task<MobileCommandResult> ExecuteAsync(OfflineCommand command, CancellationToken cancellationToken)
    {
        var task = preparedTasks.TryGetValue(command.TaskId, out var prepared)
            ? prepared
            : await db.Tasks.Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == command.TaskId, cancellationToken);
        if (task is null) return Result(command, MobileCommandStatus.Rejected, "TASK_NOT_FOUND", "Refresh assigned tasks.", null);
        if (!string.Equals(task.AssigneeId, command.UserId, StringComparison.Ordinal) || !string.Equals(task.DeviceId, command.DeviceId, StringComparison.Ordinal))
            return Result(command, MobileCommandStatus.Unauthorized, "TASK_NOT_ASSIGNED", "Reauthenticate and refresh tasks.", task.Version);
        if (task.ExpiresAt <= DateTimeOffset.UtcNow)
            return Result(command, MobileCommandStatus.Expired, "TASK_EXPIRED", "Refresh assigned tasks.", task.Version);
        try
        {
            var aggregate = new WarehouseTaskAggregate(Enum.Parse<WarehouseTaskStatus>(task.Status), task.Version);
            if (command.CommandType == "StartTask")
            {
                aggregate.Start(command.EntityVersion, task.ExpiresAt, DateTimeOffset.UtcNow);
                task.Status = aggregate.Status.ToString(); task.Version = aggregate.Version; task.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return Result(command, MobileCommandStatus.Accepted, "TASK_STARTED", "Continue task execution.", task.Version);
            }
            if (command.CommandType is "ConfirmReceipt" or "ConfirmPutaway" or "ConfirmPick")
            {
                var handler = handlers.SingleOrDefault(candidate => candidate.CanHandle(task, command));
                return handler is null
                    ? Result(command, MobileCommandStatus.RequiresReview, "OWNER_HANDLER_REQUIRED", "Keep the task frozen until the owning module processes the physical command.", task.Version)
                    : await handler.ExecuteAsync(task, command, cancellationToken);
            }
            return Result(command, MobileCommandStatus.Rejected, "COMMAND_TYPE_UNSUPPORTED", "Update the mobile client.", task.Version);
        }
        catch (WmsProblemException ex)
        {
            return Result(command, MobileCommandStatus.Conflict, ex.Code, "Refresh task and request supervisor review.", task.Version);
        }
    }

    private static MobileCommandResult Result(OfflineCommand c, MobileCommandStatus status, string code, string action, long? version) =>
        new(c.CommandId, status, code, code.Replace('_', ' ').ToLowerInvariant(), version, action);
}
