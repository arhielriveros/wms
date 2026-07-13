using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wms.BuildingBlocks;
using Wms.Inbound;
using Wms.Integration;
using Wms.Inventory;
using Wms.Layout;
using Wms.MasterData;
using Wms.MobileSync;
using Wms.Outbound;
using Wms.SecurityAudit;
using Wms.TaskExecution;

namespace Wms.Api;

public sealed class PhysicalTaskCommandHandler(
    TaskExecutionDbContext tasks,
    InboundDbContext inbound,
    OutboundDbContext outbound,
    InventoryDbContext inventory,
    LayoutDbContext layout,
    MasterDataDbContext masterData,
    IntegrationDbContext integration,
    MobileSyncDbContext mobileSync,
    SecurityAuditDbContext audit,
    IInventoryCommands inventoryCommands,
    ITenantContext tenant,
    IConfiguration configuration) : IOfflineTaskCommandHandler
{
    public bool CanHandle(WarehouseTask task, OfflineCommand command) =>
        (task.Type, command.CommandType) is
            ("Receive", "ConfirmReceipt") or
            ("Putaway", "ConfirmPutaway") or
            ("Pick", "ConfirmPick");

    public async Task<MobileCommandResult> ExecuteAsync(WarehouseTask task, OfflineCommand command, CancellationToken cancellationToken)
    {
        TaskExecutionPayload payload;
        try
        {
            payload = command.Payload.Deserialize<TaskExecutionPayload>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new JsonException("Payload is required.");
        }
        catch (JsonException)
        {
            return Result(command, MobileCommandStatus.Rejected, "PAYLOAD_INVALID", "Scan and quantity are required.", task.Version, "Correct the command payload.");
        }

        if (payload.Quantity <= 0 || string.IsNullOrWhiteSpace(payload.Barcode))
            return Result(command, MobileCommandStatus.Rejected, "PAYLOAD_INVALID", "Scan and positive quantity are required.", task.Version, "Correct the command payload.");

        await inventory.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await inventory.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var connection = inventory.Database.GetDbConnection();
            var dbTransaction = transaction.GetDbTransaction();
            await JoinAsync(tasks, connection, dbTransaction, cancellationToken);
            await JoinAsync(inbound, connection, dbTransaction, cancellationToken);
            await JoinAsync(outbound, connection, dbTransaction, cancellationToken);
            await JoinAsync(layout, connection, dbTransaction, cancellationToken);
            await JoinAsync(masterData, connection, dbTransaction, cancellationToken);
            await JoinAsync(integration, connection, dbTransaction, cancellationToken);
            await JoinAsync(mobileSync, connection, dbTransaction, cancellationToken);
            await JoinAsync(audit, connection, dbTransaction, cancellationToken);

            var result = command.CommandType switch
            {
                "ConfirmReceipt" => await ConfirmReceiptAsync(task, command, payload, cancellationToken),
                "ConfirmPutaway" => await ConfirmPutawayAsync(task, command, payload, cancellationToken),
                "ConfirmPick" => await ConfirmPickAsync(task, command, payload, cancellationToken),
                _ => Result(command, MobileCommandStatus.Rejected, "COMMAND_TYPE_UNSUPPORTED", "Command type is unsupported.", task.Version, "Update the mobile client.")
            };
            if (result.Status == MobileCommandStatus.Accepted)
            {
                mobileSync.CommandInbox.Add(NewEntity(new CommandInbox
                {
                    CommandId = command.CommandId, TaskId = command.TaskId, LocalSequence = command.LocalSequence,
                    PayloadChecksum = PayloadChecksum.Compute(command), ResultStatus = result.Status.ToString(),
                    ResultCode = result.Code, ResultMessage = result.Message, CurrentVersion = result.CurrentVersion,
                    SuggestedAction = result.SuggestedAction
                }, command));
                await mobileSync.SaveChangesAsync(cancellationToken);
                audit.AuditRecords.Add(NewEntity(new AuditRecord
                {
                    ActorId = tenant.ActorId, Action = command.CommandType, ObjectType = "WarehouseTask",
                    ObjectId = task.Id.ToString(), Result = "Accepted",
                    Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { command.CommandId, task.Type, command.WarehouseId, result.Code }))
                }, command));
                await audit.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            else await transaction.RollbackAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result(command, MobileCommandStatus.Conflict, "CONCURRENT_CHANGE", "Stock or task changed concurrently.", task.Version, "Refresh the task and request review.");
        }
        catch (WmsProblemException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            var status = exception.Code == "PICK_QUANTITY_MISMATCH"
                ? MobileCommandStatus.RequiresReview
                : exception.StatusCode == 409 ? MobileCommandStatus.Conflict : MobileCommandStatus.Rejected;
            return Result(command, status, exception.Code, exception.Message, task.Version, status == MobileCommandStatus.Conflict ? "Refresh the task and request review." : "Correct the scan or quantity.");
        }
        finally
        {
            await LeaveAsync(tasks);
            await LeaveAsync(inbound);
            await LeaveAsync(outbound);
            await LeaveAsync(layout);
            await LeaveAsync(masterData);
            await LeaveAsync(integration);
            await LeaveAsync(mobileSync);
            await LeaveAsync(audit);
            await inventory.Database.CloseConnectionAsync();
        }
    }

    private async Task<MobileCommandResult> ConfirmReceiptAsync(WarehouseTask task, OfflineCommand command, TaskExecutionPayload payload, CancellationToken ct)
    {
        var line = await inbound.AsnLines.SingleOrDefaultAsync(x => x.Id == task.OwnerEntityId, ct)
            ?? throw new WmsProblemException("ASN_LINE_NOT_FOUND", "ASN line was not found.", 404);
        var asn = await inbound.Asns.SingleAsync(x => x.Id == line.AsnId, ct);
        var owner = await masterData.Owners.SingleAsync(x => x.Code == asn.OwnerCode, ct);
        var sku = await masterData.Skus.SingleAsync(x => x.OwnerId == owner.Id && x.Code == line.Sku, ct);
        ValidateBarcode(payload.Barcode, sku.Barcode, line.Sku);
        var remaining = line.ExpectedQuantity - line.ReceivedQuantity;
        RequireExactQuantity(payload.Quantity, remaining, "RECEIPT_QUANTITY_MISMATCH");
        var destination = await ResolveLocationAsync(task.WarehouseId, payload.DestinationLocation, "Destination", "Staging", task, ct);

        await inventoryCommands.ReceiveAsync(new ReceiveStockCommand(task.WarehouseId, owner.Id, line.Sku, destination.Id, "Available", payload.Quantity, line.Uom, command.CommandId, null), ct);
        line.ReceivedQuantity += payload.Quantity;
        line.Version++;
        asn.Status = (await inbound.AsnLines.Where(x => x.AsnId == asn.Id).AllAsync(x => x.Id == line.Id || x.ReceivedQuantity == x.ExpectedQuantity, ct))
            ? nameof(AsnStatus.Received)
            : nameof(AsnStatus.Receiving);
        asn.Version++;

        var receipt = await inbound.Receipts.SingleOrDefaultAsync(x => x.AsnId == asn.Id, ct);
        if (receipt is null)
        {
            receipt = NewEntity(new Receipt { AsnId = asn.Id }, command);
            inbound.Receipts.Add(receipt);
        }
        if (asn.Status == nameof(AsnStatus.Received)) receipt.Status = "Received";

        var storage = await layout.Locations.Where(x => x.WarehouseId == task.WarehouseId && x.Type == "Storage" && x.IsActive)
            .OrderBy(x => x.Code).FirstOrDefaultAsync(ct)
            ?? throw new WmsProblemException("STORAGE_LOCATION_NOT_FOUND", "No storage location is configured.", 409);
        tasks.Tasks.Add(NewAssignedTask("Putaway", $"{asn.Id}/{line.ExternalLineId}", line.Id, task.WarehouseId, task, command,
            [
                NewStep(1, "ScanSource", destination.Code, sku.Barcode, payload.Quantity, line.Uom, command),
                NewStep(2, "ScanDestination", storage.Code, sku.Barcode, payload.Quantity, line.Uom, command)
            ]));
        CompleteTask(task, command.EntityVersion);
        await inbound.SaveChangesAsync(ct);
        await tasks.SaveChangesAsync(ct);
        return Result(command, MobileCommandStatus.Accepted, "RECEIPT_CONFIRMED", "Receipt recorded and putaway assigned.", task.Version, "Continue with the putaway task.");
    }

    private async Task<MobileCommandResult> ConfirmPutawayAsync(WarehouseTask task, OfflineCommand command, TaskExecutionPayload payload, CancellationToken ct)
    {
        var line = await inbound.AsnLines.SingleOrDefaultAsync(x => x.Id == task.OwnerEntityId, ct)
            ?? throw new WmsProblemException("ASN_LINE_NOT_FOUND", "ASN line was not found.", 404);
        var asn = await inbound.Asns.SingleAsync(x => x.Id == line.AsnId, ct);
        var owner = await masterData.Owners.SingleAsync(x => x.Code == asn.OwnerCode, ct);
        var sku = await masterData.Skus.SingleAsync(x => x.OwnerId == owner.Id && x.Code == line.Sku, ct);
        ValidateBarcode(payload.Barcode, sku.Barcode, line.Sku);
        var remaining = line.ReceivedQuantity - line.PutawayQuantity;
        RequireExactQuantity(payload.Quantity, remaining, "PUTAWAY_QUANTITY_MISMATCH");
        var source = await ResolveLocationAsync(task.WarehouseId, payload.SourceLocation, "Source", "Staging", task, ct);
        var destination = await ResolveLocationAsync(task.WarehouseId, payload.DestinationLocation, "Destination", "Storage", task, ct);

        await inventoryCommands.TransferAsync(new TransferStockCommand(task.WarehouseId, owner.Id, line.Sku, source.Id, destination.Id, "Available", payload.Quantity, line.Uom, command.CommandId), ct);
        line.PutawayQuantity += payload.Quantity;
        line.Version++;
        var allClosed = await inbound.AsnLines.Where(x => x.AsnId == asn.Id).AllAsync(x => x.Id == line.Id || x.PutawayQuantity == x.ExpectedQuantity, ct);
        asn.Status = allClosed ? nameof(AsnStatus.Completed) : nameof(AsnStatus.PutawayInProgress);
        asn.Version++;
        if (allClosed)
        {
            var receipt = await inbound.Receipts.SingleAsync(x => x.AsnId == asn.Id, ct);
            receipt.Status = "Completed";
            receipt.CompletedAt = DateTimeOffset.UtcNow;
            receipt.Version++;
            integration.Outbox.Add(NewConfirmation("ReceiptConfirmation", command.CommandId, asn.ExternalId, command));
        }
        CompleteTask(task, command.EntityVersion);
        await inbound.SaveChangesAsync(ct);
        await tasks.SaveChangesAsync(ct);
        await integration.SaveChangesAsync(ct);
        return Result(command, MobileCommandStatus.Accepted, "PUTAWAY_CONFIRMED", "Putaway and stock transfer recorded.", task.Version, "Return to assigned tasks.");
    }

    private async Task<MobileCommandResult> ConfirmPickAsync(WarehouseTask task, OfflineCommand command, TaskExecutionPayload payload, CancellationToken ct)
    {
        var line = await outbound.OrderLines.SingleOrDefaultAsync(x => x.Id == task.OwnerEntityId, ct)
            ?? throw new WmsProblemException("ORDER_LINE_NOT_FOUND", "Order line was not found.", 404);
        var order = await outbound.SalesOrders.SingleAsync(x => x.Id == line.SalesOrderId, ct);
        var owner = await masterData.Owners.SingleAsync(x => x.Code == order.OwnerCode, ct);
        var sku = await masterData.Skus.SingleAsync(x => x.OwnerId == owner.Id && x.Code == line.Sku, ct);
        ValidateBarcode(payload.Barcode, sku.Barcode, line.Sku);
        var remaining = line.AllocatedQuantity - line.PickedQuantity;
        var taskQuantity = task.Steps.Select(x => x.Quantity).DefaultIfEmpty(0).Max();
        RequireExactQuantity(payload.Quantity, taskQuantity, "PICK_QUANTITY_MISMATCH");
        if (payload.Quantity > remaining) throw new WmsProblemException("PICK_QUANTITY_MISMATCH", $"Remaining allocated quantity is {remaining}.", 409);
        var source = await ResolveLocationAsync(task.WarehouseId, payload.SourceLocation, "Source", "Storage", task, ct);

        await inventoryCommands.PickAsync(new PickStockCommand(task.WarehouseId, owner.Id, line.Sku, source.Id, order.Id, line.Id, payload.Quantity, line.Uom, command.CommandId), ct);
        line.PickedQuantity += payload.Quantity;
        line.Version++;
        order.Status = nameof(OrderStatus.Picking);
        order.Version++;
        CompleteTask(task, command.EntityVersion);
        await outbound.SaveChangesAsync(ct);
        await tasks.SaveChangesAsync(ct);
        return Result(command, MobileCommandStatus.Accepted, "PICK_CONFIRMED", "Pick and reserved stock consumption recorded.", task.Version, "Proceed to online packing.");
    }

    private async Task<Location> ResolveLocationAsync(Guid warehouseId, string? payloadCode, string actionToken, string expectedType, WarehouseTask task, CancellationToken ct)
    {
        var code = payloadCode ?? task.Steps.OrderBy(x => x.Sequence).FirstOrDefault(x => x.Action.Contains(actionToken, StringComparison.OrdinalIgnoreCase))?.LocationBarcode;
        if (string.IsNullOrWhiteSpace(code)) throw new WmsProblemException("LOCATION_REQUIRED", $"{actionToken} location is required.");
        var location = await layout.Locations.SingleOrDefaultAsync(x => x.WarehouseId == warehouseId && x.Code == code && x.IsActive, ct)
            ?? throw new WmsProblemException("LOCATION_NOT_FOUND", $"Location {code} was not found.", 404);
        if (!string.Equals(location.Type, expectedType, StringComparison.OrdinalIgnoreCase))
            throw new WmsProblemException("LOCATION_TYPE_INVALID", $"Location {code} is not a valid {expectedType} location.", 409);
        return location;
    }

    private static void CompleteTask(WarehouseTask task, long expectedVersion)
    {
        var aggregate = new WarehouseTaskAggregate(Enum.Parse<WarehouseTaskStatus>(task.Status), task.Version);
        if (aggregate.Status == WarehouseTaskStatus.Assigned)
        {
            aggregate.Start(expectedVersion, task.ExpiresAt, DateTimeOffset.UtcNow);
            expectedVersion = aggregate.Version;
        }
        aggregate.Complete(expectedVersion, ownerEffectConfirmed: true);
        task.Status = aggregate.Status.ToString();
        task.Version = aggregate.Version;
        task.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateBarcode(string actual, string expectedBarcode, string expectedSku)
    {
        if (!string.Equals(actual, expectedBarcode, StringComparison.OrdinalIgnoreCase) && !string.Equals(actual, expectedSku, StringComparison.OrdinalIgnoreCase))
            throw new WmsProblemException("BARCODE_MISMATCH", "Scanned barcode does not match the assigned SKU.", 409);
    }

    private static void RequireExactQuantity(decimal actual, decimal expected, string code)
    {
        if (expected <= 0 || actual != expected) throw new WmsProblemException(code, $"Expected quantity is {expected}.", 409);
    }

    private WarehouseTask NewAssignedTask(string type, string reference, Guid ownerEntityId, Guid warehouseId, WarehouseTask sourceTask, OfflineCommand command, IReadOnlyList<WarehouseTaskStep> steps)
    {
        var task = NewEntity(new WarehouseTask
        {
            WarehouseId = warehouseId, Type = type, Reference = reference, OwnerEntityId = ownerEntityId,
            AssigneeId = sourceTask.AssigneeId, DeviceId = sourceTask.DeviceId, ZoneCode = sourceTask.ZoneCode,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(8), Status = nameof(WarehouseTaskStatus.Assigned),
            Priority = sourceTask.Priority, UpdatedAt = DateTimeOffset.UtcNow
        }, command);
        foreach (var step in steps) { step.TaskId = task.Id; task.Steps.Add(step); }
        return task;
    }

    private WarehouseTaskStep NewStep(int sequence, string action, string? location, string? sku, decimal quantity, string uom, OfflineCommand command) =>
        NewEntity(new WarehouseTaskStep { Sequence = sequence, Action = action, LocationBarcode = location, SkuBarcode = sku, Quantity = quantity, Uom = uom }, command);

    private T NewEntity<T>(T entity, OfflineCommand command) where T : TenantEntity
    {
        entity.TenantId = tenant.TenantId;
        entity.CreatedBy = tenant.ActorId;
        entity.CorrelationId = tenant.CorrelationId;
        return entity;
    }

    private OutboxMessage NewConfirmation(string type, Guid causationId, string externalId, OfflineCommand command) => NewEntity(new OutboxMessage
    {
        MessageId = Guid.NewGuid(), MessageType = type, CausationId = causationId, DeliveryKind = "Webhook",
        Destination = configuration[$"Integration:{type}WebhookUrl"],
        Payload = JsonDocument.Parse(JsonSerializer.Serialize(new { externalId, occurredAt = DateTimeOffset.UtcNow, correlationId = tenant.CorrelationId }))
    }, command);

    private static async Task JoinAsync(DbContext context, DbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        if (!ReferenceEquals(context.Database.GetDbConnection(), connection)) context.Database.SetDbConnection(connection, contextOwnsConnection: false);
        await context.Database.UseTransactionAsync(transaction, ct);
    }

    private static async Task LeaveAsync(DbContext context)
    {
        if (context.Database.CurrentTransaction is not null)
            await context.Database.UseTransactionAsync(null, CancellationToken.None);
    }

    private static MobileCommandResult Result(OfflineCommand command, MobileCommandStatus status, string code, string message, long? version, string action) =>
        new(command.CommandId, status, code, message, version, action);

    private sealed record TaskExecutionPayload(string Barcode, decimal Quantity, string? SourceLocation, string? DestinationLocation);
}
