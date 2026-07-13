using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;
using Wms.Inbound;
using Wms.Layout;
using Wms.MasterData;
using Wms.TaskExecution;

namespace Wms.Worker;

public interface IImportTaskPlanner
{
    Task PlanInboundAsync(Guid asnId, Guid tenantId, Guid correlationId, CancellationToken cancellationToken);
}

public sealed class ImportTaskPlanner(
    InboundDbContext inbound,
    LayoutDbContext layout,
    MasterDataDbContext masterData,
    TaskExecutionDbContext tasks,
    IConfiguration configuration) : IImportTaskPlanner
{
    public async Task PlanInboundAsync(Guid asnId, Guid tenantId, Guid correlationId, CancellationToken cancellationToken)
    {
        if (await tasks.Tasks.AnyAsync(x => x.Reference.StartsWith(asnId.ToString()), cancellationToken)) return;
        var asn = await inbound.Asns.Include(x => x.Lines).SingleAsync(x => x.Id == asnId, cancellationToken);
        var warehouse = await layout.Warehouses.SingleOrDefaultAsync(x => x.Code == asn.WarehouseCode && x.IsActive, cancellationToken)
            ?? throw new WmsProblemException("WAREHOUSE_NOT_FOUND", $"Warehouse {asn.WarehouseCode} is not configured.", 409);
        var owner = await masterData.Owners.SingleOrDefaultAsync(x => x.Code == asn.OwnerCode, cancellationToken)
            ?? throw new WmsProblemException("OWNER_NOT_FOUND", $"Owner {asn.OwnerCode} is not configured.", 409);
        var staging = await layout.Locations.Where(x => x.WarehouseId == warehouse.Id && x.Type == "Staging" && x.IsActive)
            .OrderBy(x => x.Code).FirstOrDefaultAsync(cancellationToken)
            ?? throw new WmsProblemException("STAGING_LOCATION_NOT_FOUND", "No staging location is configured.", 409);
        var assignee = configuration["Pilot:OperatorUserId"] ?? "operator-01";
        var device = configuration["Pilot:DeviceId"] ?? "zebra-01";

        foreach (var line in asn.Lines.OrderBy(x => x.ExternalLineId))
        {
            var sku = await masterData.Skus.SingleOrDefaultAsync(x => x.OwnerId == owner.Id && x.Code == line.Sku, cancellationToken)
                ?? throw new WmsProblemException("SKU_NOT_FOUND", $"SKU {line.Sku} is not configured.", 409);
            var task = NewEntity(new WarehouseTask
            {
                TenantId = tenantId, WarehouseId = warehouse.Id, Type = "Receive", Reference = $"{asnId}/{line.ExternalLineId}",
                OwnerEntityId = line.Id, AssigneeId = assignee, DeviceId = device, ZoneCode = staging.ZoneCode,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(8), Status = nameof(WarehouseTaskStatus.Assigned),
                Priority = 100, UpdatedAt = DateTimeOffset.UtcNow
            }, tenantId, correlationId);
            task.Steps.Add(NewStep(task.Id, 1, "ScanSku", null, sku.Barcode, line.ExpectedQuantity, line.Uom, tenantId, correlationId));
            task.Steps.Add(NewStep(task.Id, 2, "ScanDestination", staging.Code, sku.Barcode, line.ExpectedQuantity, line.Uom, tenantId, correlationId));
            tasks.Tasks.Add(task);
        }
        await tasks.SaveChangesAsync(cancellationToken);
    }

    private static WarehouseTaskStep NewStep(Guid taskId, int sequence, string action, string? location, string? sku, decimal quantity, string uom, Guid tenantId, Guid correlationId) =>
        NewEntity(new WarehouseTaskStep { TaskId = taskId, Sequence = sequence, Action = action, LocationBarcode = location, SkuBarcode = sku, Quantity = quantity, Uom = uom }, tenantId, correlationId);

    private static T NewEntity<T>(T entity, Guid tenantId, Guid correlationId) where T : TenantEntity
    {
        entity.TenantId = tenantId;
        entity.CreatedBy = "wms-worker";
        entity.CorrelationId = correlationId;
        return entity;
    }
}
