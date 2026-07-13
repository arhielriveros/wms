using System.Text.Json;

namespace Wms.BuildingBlocks;

public enum MobileCommandStatus
{
    Accepted,
    Rejected,
    Conflict,
    AlreadyProcessed,
    RequiresReview,
    Expired,
    Unauthorized
}

public sealed record OfflineCommand(
    Guid CommandId,
    string CommandType,
    string SchemaVersion,
    Guid TenantId,
    Guid WarehouseId,
    string DeviceId,
    string UserId,
    DateTimeOffset OccurredAt,
    long LocalSequence,
    long EntityVersion,
    Guid TaskId,
    JsonElement Payload);

public sealed record MobileCommandResult(
    Guid CommandId,
    MobileCommandStatus Status,
    string Code,
    string Message,
    long? CurrentVersion,
    string SuggestedAction);

public sealed record MobileCommandBatch(IReadOnlyList<OfflineCommand> Commands);

public sealed record AssignedTaskDto(
    Guid Id,
    string Type,
    string Reference,
    string Status,
    long Version,
    DateTimeOffset? ExpiresAt,
    int Priority,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<TaskStepDto> Steps);

public sealed record TaskStepDto(int Sequence, string Action, string? LocationBarcode, string? SkuBarcode, decimal Quantity, string Uom);

public sealed record MobileBootstrapDto(
    Guid TenantId,
    Guid WarehouseId,
    string WarehouseCode,
    string DeviceId,
    string UserId,
    long Checkpoint,
    DateTimeOffset ServerTime,
    IReadOnlyList<string> Scopes);
