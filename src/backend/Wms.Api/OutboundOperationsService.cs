using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wms.BuildingBlocks;
using Wms.Integration;
using Wms.Inventory;
using Wms.Layout;
using Wms.MasterData;
using Wms.MobileSync;
using Wms.Outbound;
using Wms.SecurityAudit;
using Wms.TaskExecution;

namespace Wms.Api;

public sealed record ReleaseOrderRequest(Guid CommandId, long EntityVersion, string? AssigneeId, string? DeviceId);
public sealed record OnlineOrderCommand(Guid CommandId, long EntityVersion);
public sealed record ShortPickDecisionRequest(Guid CommandId, Guid MobileCommandId, Guid TaskId, long TaskEntityVersion, decimal ActualQuantity, string Reason, bool Approve);
public sealed record OutboundOperationResult(Guid OrderId, string Status, long EntityVersion, bool AlreadyProcessed);

public interface IOutboundOperations
{
    Task<OutboundOperationResult> ReleaseAsync(Guid orderId, ReleaseOrderRequest request, CancellationToken cancellationToken);
    Task<OutboundOperationResult> PackAsync(Guid orderId, OnlineOrderCommand request, CancellationToken cancellationToken);
    Task<OutboundOperationResult> DispatchAsync(Guid orderId, OnlineOrderCommand request, CancellationToken cancellationToken);
    Task<OutboundOperationResult> DecideShortPickAsync(Guid orderId, Guid lineId, ShortPickDecisionRequest request, CancellationToken cancellationToken);
}

public sealed class OutboundOperationsService(
    OutboundDbContext outbound,
    InventoryDbContext inventory,
    LayoutDbContext layout,
    MasterDataDbContext masterData,
    TaskExecutionDbContext tasks,
    IntegrationDbContext integration,
    SecurityAuditDbContext audit,
    MobileSyncDbContext mobileSync,
    IInventoryCommands inventoryCommands,
    ITenantContext tenant,
    IConfiguration configuration) : IOutboundOperations
{
    public async Task<OutboundOperationResult> ReleaseAsync(Guid orderId, ReleaseOrderRequest request, CancellationToken ct)
    {
        if (request.CommandId == Guid.Empty) throw new WmsProblemException("COMMAND_ID_REQUIRED", "CommandId is required.");
        await inventory.Database.OpenConnectionAsync(ct);
        await using var transaction = await inventory.Database.BeginTransactionAsync(ct);
        try
        {
            var connection = inventory.Database.GetDbConnection();
            var dbTransaction = transaction.GetDbTransaction();
            await JoinAsync(outbound, connection, dbTransaction, ct);
            await JoinAsync(layout, connection, dbTransaction, ct);
            await JoinAsync(masterData, connection, dbTransaction, ct);
            await JoinAsync(tasks, connection, dbTransaction, ct);
            await JoinAsync(audit, connection, dbTransaction, ct);

            var order = await outbound.SalesOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == orderId, ct)
                ?? throw new WmsProblemException("ORDER_NOT_FOUND", "Sales order was not found.", 404);
            if (order.Status is nameof(OrderStatus.Released) or nameof(OrderStatus.Picking) or nameof(OrderStatus.Packed) or nameof(OrderStatus.Shipped))
                return new(order.Id, order.Status, order.Version, true);
            if (order.Status != nameof(OrderStatus.Imported)) throw new WmsProblemException("OUT_INVALID_TRANSITION", "Order cannot be released in its current state.", 409);
            if (order.Version != request.EntityVersion) throw new WmsProblemException("ORDER_STATE_CONFLICT", "Order version has changed.", 409);

            var warehouse = await layout.Warehouses.SingleOrDefaultAsync(x => x.Code == order.WarehouseCode && x.IsActive, ct)
                ?? throw new WmsProblemException("WAREHOUSE_NOT_FOUND", $"Warehouse {order.WarehouseCode} is not configured.", 409);
            var owner = await masterData.Owners.SingleOrDefaultAsync(x => x.Code == order.OwnerCode, ct)
                ?? throw new WmsProblemException("OWNER_NOT_FOUND", $"Owner {order.OwnerCode} is not configured.", 409);
            var assignee = request.AssigneeId ?? configuration["Pilot:OperatorUserId"] ?? "operator-01";
            var device = request.DeviceId ?? configuration["Pilot:DeviceId"] ?? "zebra-01";

            foreach (var line in order.Lines.OrderBy(x => x.ExternalLineId))
            {
                var sku = await masterData.Skus.SingleOrDefaultAsync(x => x.OwnerId == owner.Id && x.Code == line.Sku, ct)
                    ?? throw new WmsProblemException("SKU_NOT_FOUND", $"SKU {line.Sku} is not configured.", 409);
                var lineCommandId = DeriveCommandId(request.CommandId, line.Id);
                var reservations = await inventoryCommands.ReserveFifoAsync(new ReserveStockCommand(warehouse.Id, owner.Id, line.Sku, order.Id, line.Id, line.OrderedQuantity, lineCommandId), ct);
                line.AllocatedQuantity = reservations.Sum(x => x.Quantity);
                line.Version++;
                foreach (var reservation in reservations)
                {
                    var stock = await inventory.StockBalances.SingleAsync(x => x.Id == reservation.StockDimensionId, ct);
                    var source = await layout.Locations.SingleAsync(x => x.Id == stock.LocationId, ct);
                    var task = NewEntity(new WarehouseTask
                    {
                        WarehouseId = warehouse.Id, Type = "Pick", Reference = $"{order.Id}/{line.ExternalLineId}/{reservation.Id}",
                        OwnerEntityId = line.Id, AssigneeId = assignee, DeviceId = device, ZoneCode = source.ZoneCode,
                        ExpiresAt = DateTimeOffset.UtcNow.AddHours(8), Status = nameof(WarehouseTaskStatus.Assigned),
                        Priority = order.Priority, UpdatedAt = DateTimeOffset.UtcNow
                    });
                    task.Steps.Add(NewEntity(new WarehouseTaskStep
                    {
                        TaskId = task.Id, Sequence = 1, Action = "ScanSource", LocationBarcode = source.Code,
                        SkuBarcode = sku.Barcode, Quantity = reservation.Quantity, Uom = line.Uom
                    }));
                    tasks.Tasks.Add(task);
                }
            }

            var aggregate = new SalesOrderAggregate();
            aggregate.Allocate(request.EntityVersion);
            aggregate.Release(aggregate.Version);
            order.Status = aggregate.Status.ToString();
            order.Version = aggregate.Version;
            await outbound.SaveChangesAsync(ct);
            await tasks.SaveChangesAsync(ct);
            RecordAudit("ReleaseOrder", "SalesOrder", order.Id, new { request.CommandId, order.ExternalId, order.Status });
            await audit.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(order.Id, order.Status, order.Version, false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(ct);
            throw new WmsProblemException("CONCURRENT_CHANGE", "Stock or order changed concurrently.", 409, new Dictionary<string, string[]> { ["detail"] = [exception.Message] });
        }
        finally { await inventory.Database.CloseConnectionAsync(); }
    }

    public async Task<OutboundOperationResult> PackAsync(Guid orderId, OnlineOrderCommand request, CancellationToken ct)
    {
        await outbound.Database.OpenConnectionAsync(ct);
        await using var transaction = await outbound.Database.BeginTransactionAsync(ct);
        try
        {
            await JoinAsync(audit, outbound.Database.GetDbConnection(), transaction.GetDbTransaction(), ct);
            var order = await outbound.SalesOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == orderId, ct)
                ?? throw new WmsProblemException("ORDER_NOT_FOUND", "Sales order was not found.", 404);
            if (order.Status == nameof(OrderStatus.Packed)) return new(order.Id, order.Status, order.Version, true);
            if (order.Lines.Any(x => x.PickedQuantity != x.AllocatedQuantity || x.PickedQuantity + x.ShortPickedQuantity != x.OrderedQuantity))
                throw new WmsProblemException("PICK_NOT_COMPLETE", "All order lines must be fully picked before packing.", 409);
            if (order.Version != request.EntityVersion) throw new WmsProblemException("ORDER_STATE_CONFLICT", "Order version has changed.", 409);
            var aggregate = new SalesOrderAggregate(Enum.Parse<OrderStatus>(order.Status), order.Version);
            aggregate.Pack(request.EntityVersion, online: true);
            order.Status = aggregate.Status.ToString();
            order.Version = aggregate.Version;
            var shipment = NewEntity(new Shipment { SalesOrderId = order.Id, Status = "Packed" });
            outbound.Shipments.Add(shipment);
            await outbound.SaveChangesAsync(ct);
            RecordAudit("PackOrder", "SalesOrder", order.Id, new { request.CommandId, order.ExternalId, order.Status });
            await audit.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(order.Id, order.Status, order.Version, false);
        }
        finally { await outbound.Database.CloseConnectionAsync(); }
    }

    public async Task<OutboundOperationResult> DispatchAsync(Guid orderId, OnlineOrderCommand request, CancellationToken ct)
    {
        await outbound.Database.OpenConnectionAsync(ct);
        await using var transaction = await outbound.Database.BeginTransactionAsync(ct);
        try
        {
            await JoinAsync(integration, outbound.Database.GetDbConnection(), transaction.GetDbTransaction(), ct);
            await JoinAsync(audit, outbound.Database.GetDbConnection(), transaction.GetDbTransaction(), ct);
            var order = await outbound.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct)
                ?? throw new WmsProblemException("ORDER_NOT_FOUND", "Sales order was not found.", 404);
            if (order.Status == nameof(OrderStatus.Shipped)) return new(order.Id, order.Status, order.Version, true);
            if (order.Status != nameof(OrderStatus.Packed)) throw new WmsProblemException("OUT_INVALID_TRANSITION", "Only a packed order can be dispatched.", 409);
            if (order.Version != request.EntityVersion) throw new WmsProblemException("ORDER_STATE_CONFLICT", "Order version has changed.", 409);
            var shipment = await outbound.Shipments.SingleAsync(x => x.SalesOrderId == order.Id, ct);
            var aggregate = new SalesOrderAggregate(Enum.Parse<OrderStatus>(order.Status), order.Version);
            aggregate.Dispatch(request.EntityVersion, online: true);
            order.Status = aggregate.Status.ToString();
            order.Version = aggregate.Version;
            shipment.Status = "Shipped";
            shipment.DispatchedAt = DateTimeOffset.UtcNow;
            shipment.Version++;
            integration.Outbox.Add(NewEntity(new OutboxMessage
            {
                MessageId = Guid.NewGuid(), MessageType = "ShipmentConfirmation", CausationId = request.CommandId,
                DeliveryKind = "Webhook", Destination = configuration["Integration:ShipmentConfirmationWebhookUrl"],
                Payload = JsonDocument.Parse(JsonSerializer.Serialize(new { externalId = order.ExternalId, dispatchedAt = shipment.DispatchedAt, correlationId = tenant.CorrelationId }))
            }));
            await outbound.SaveChangesAsync(ct);
            await integration.SaveChangesAsync(ct);
            RecordAudit("DispatchOrder", "SalesOrder", order.Id, new { request.CommandId, order.ExternalId, order.Status });
            await audit.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(order.Id, order.Status, order.Version, false);
        }
        finally { await outbound.Database.CloseConnectionAsync(); }
    }

    public async Task<OutboundOperationResult> DecideShortPickAsync(Guid orderId, Guid lineId, ShortPickDecisionRequest request, CancellationToken ct)
    {
        if (request.CommandId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason))
            throw new WmsProblemException("SHORT_PICK_REASON_REQUIRED", "CommandId and reason are required.");
        await inventory.Database.OpenConnectionAsync(ct);
        await using var transaction = await inventory.Database.BeginTransactionAsync(ct);
        try
        {
            var connection = inventory.Database.GetDbConnection();
            var dbTransaction = transaction.GetDbTransaction();
            await JoinAsync(outbound, connection, dbTransaction, ct);
            await JoinAsync(layout, connection, dbTransaction, ct);
            await JoinAsync(masterData, connection, dbTransaction, ct);
            await JoinAsync(tasks, connection, dbTransaction, ct);
            await JoinAsync(audit, connection, dbTransaction, ct);
            await JoinAsync(mobileSync, connection, dbTransaction, ct);

            var order = await outbound.SalesOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == orderId, ct)
                ?? throw new WmsProblemException("ORDER_NOT_FOUND", "Sales order was not found.", 404);
            var line = order.Lines.SingleOrDefault(x => x.Id == lineId)
                ?? throw new WmsProblemException("ORDER_LINE_NOT_FOUND", "Sales order line was not found.", 404);
            var task = await tasks.Tasks.Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == request.TaskId && x.OwnerEntityId == line.Id && x.Type == "Pick", ct)
                ?? throw new WmsProblemException("TASK_NOT_FOUND", "Pick task was not found.", 404);
            if (task.Status == nameof(WarehouseTaskStatus.Completed)) return new(order.Id, order.Status, order.Version, true);
            if (task.Version != request.TaskEntityVersion) throw new WmsProblemException("TASK_VERSION_CONFLICT", "Task version has changed.", 409);
            var taskQuantity = task.Steps.Select(x => x.Quantity).DefaultIfEmpty(0).Max();
            if (request.ActualQuantity < 0 || request.ActualQuantity >= taskQuantity)
                throw new WmsProblemException("SHORT_PICK_QUANTITY_INVALID", "Short-pick quantity must be lower than the assigned quantity.", 409);

            if (!request.Approve)
            {
                await ResolveMobileReviewAsync(request.MobileCommandId, "Rejected", "SHORT_PICK_REJECTED", "Supervisor rejected the short pick.", ct);
                RecordAudit("RejectShortPick", "WarehouseTask", task.Id, new { request.CommandId, request.Reason, request.ActualQuantity, taskQuantity });
                await mobileSync.SaveChangesAsync(ct);
                await audit.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new(order.Id, order.Status, order.Version, false);
            }

            var warehouse = await layout.Warehouses.SingleAsync(x => x.Id == task.WarehouseId, ct);
            var owner = await masterData.Owners.SingleAsync(x => x.Code == order.OwnerCode, ct);
            var sourceCode = task.Steps.OrderBy(x => x.Sequence).First(x => x.Action.Contains("Source", StringComparison.OrdinalIgnoreCase)).LocationBarcode;
            var source = await layout.Locations.SingleAsync(x => x.WarehouseId == warehouse.Id && x.Code == sourceCode, ct);
            var shortQuantity = taskQuantity - request.ActualQuantity;
            if (request.ActualQuantity > 0)
                await inventoryCommands.PickAsync(new PickStockCommand(warehouse.Id, owner.Id, line.Sku, source.Id, order.Id, line.Id,
                    request.ActualQuantity, line.Uom, DeriveCommandId(request.CommandId, $"{task.Id:N}:pick")), ct);
            await inventoryCommands.ReleaseReservedAsync(new ReleaseReservedStockCommand(warehouse.Id, owner.Id, line.Sku, source.Id, order.Id, line.Id,
                shortQuantity, line.Uom, DeriveCommandId(request.CommandId, $"{task.Id:N}:release")), ct);

            line.PickedQuantity += request.ActualQuantity;
            line.ShortPickedQuantity += shortQuantity;
            line.AllocatedQuantity -= shortQuantity;
            line.ShortPickReason = request.Reason;
            line.Version++;
            var aggregate = new WarehouseTaskAggregate(Enum.Parse<WarehouseTaskStatus>(task.Status), task.Version);
            if (aggregate.Status == WarehouseTaskStatus.Assigned)
            {
                aggregate.Start(request.TaskEntityVersion, task.ExpiresAt, DateTimeOffset.UtcNow);
                aggregate.Complete(aggregate.Version, ownerEffectConfirmed: true);
            }
            else aggregate.Complete(request.TaskEntityVersion, ownerEffectConfirmed: true);
            task.Status = aggregate.Status.ToString();
            task.Version = aggregate.Version;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            order.Status = nameof(OrderStatus.Picking);
            order.Version++;
            await ResolveMobileReviewAsync(request.MobileCommandId, "Accepted", "SHORT_PICK_APPROVED", "Supervisor approved the short pick.", ct);
            await outbound.SaveChangesAsync(ct);
            await tasks.SaveChangesAsync(ct);
            await mobileSync.SaveChangesAsync(ct);
            RecordAudit("ApproveShortPick", "WarehouseTask", task.Id, new { request.CommandId, request.Reason, request.ActualQuantity, shortQuantity });
            await audit.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(order.Id, order.Status, order.Version, false);
        }
        finally { await inventory.Database.CloseConnectionAsync(); }
    }

    private T NewEntity<T>(T entity) where T : TenantEntity
    {
        entity.TenantId = tenant.TenantId;
        entity.CreatedBy = tenant.ActorId;
        entity.CorrelationId = tenant.CorrelationId;
        return entity;
    }

    private void RecordAudit(string action, string objectType, Guid objectId, object metadata) => audit.AuditRecords.Add(NewEntity(new AuditRecord
    {
        ActorId = tenant.ActorId, Action = action, ObjectType = objectType, ObjectId = objectId.ToString(),
        Result = "Accepted", Metadata = JsonDocument.Parse(JsonSerializer.Serialize(metadata))
    }));

    private async Task ResolveMobileReviewAsync(Guid mobileCommandId, string status, string code, string message, CancellationToken ct)
    {
        var command = await mobileSync.CommandInbox.SingleOrDefaultAsync(x => x.CommandId == mobileCommandId, ct)
            ?? throw new WmsProblemException("MOBILE_COMMAND_NOT_FOUND", "The mobile command under review was not found.", 404);
        command.ResultStatus = status;
        command.ResultCode = code;
        command.ResultMessage = message;
        command.SuggestedAction = "Refresh assigned tasks.";
        command.Version++;
    }

    private static Guid DeriveCommandId(Guid commandId, Guid lineId) => DeriveCommandId(commandId, lineId.ToString("N"));

    private static Guid DeriveCommandId(Guid commandId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{commandId:N}:{discriminator}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static async Task JoinAsync(DbContext context, DbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        if (!ReferenceEquals(context.Database.GetDbConnection(), connection)) context.Database.SetDbConnection(connection, contextOwnsConnection: false);
        await context.Database.UseTransactionAsync(transaction, ct);
    }
}
