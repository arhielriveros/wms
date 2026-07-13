using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;

namespace Wms.Inventory;

public sealed class StockBalance : TenantEntity
{
    public Guid WarehouseId { get; set; }
    public Guid OwnerId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string Status { get; set; } = "Available";
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal Blocked { get; set; }
    public decimal Available => OnHand - Reserved - Blocked;
    public DateTimeOffset ReceivedAt { get; set; }
    public Guid LastMovementId { get; set; }
}

public sealed class InventoryMovement : TenantEntity
{
    public Guid StockDimensionId { get; set; }
    public Guid? RelatedStockDimensionId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "EA";
    public Guid CommandId { get; set; }
    public string PayloadChecksum { get; set; } = string.Empty;
    public Guid? CompensatesMovementId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class InventoryReservation : TenantEntity
{
    public Guid StockDimensionId { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderLineId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public decimal ReleasedQuantity { get; set; }
    public string Status { get; set; } = "Active";
    public Guid CommandId { get; set; }
    public string PayloadChecksum { get; set; } = string.Empty;
}

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<InventoryMovement> Movements => Set<InventoryMovement>();
    public DbSet<InventoryReservation> Reservations => Set<InventoryReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stock = modelBuilder.Entity<StockBalance>();
        stock.ToTable("stock_balance", "inventory", t =>
        {
            t.HasCheckConstraint("ck_stock_non_negative", "on_hand >= 0 AND reserved >= 0 AND blocked >= 0");
            t.HasCheckConstraint("ck_stock_available", "reserved + blocked <= on_hand");
        });
        stock.HasKey(x => x.Id);
        stock.HasIndex(x => new { x.TenantId, x.WarehouseId, x.OwnerId, x.Sku, x.LocationId, x.Status }).IsUnique();
        stock.Property(x => x.Sku).HasMaxLength(100);
        stock.Property(x => x.Status).HasMaxLength(32);
        stock.Property(x => x.OnHand).HasPrecision(18, 4);
        stock.Property(x => x.Reserved).HasPrecision(18, 4);
        stock.Property(x => x.Blocked).HasPrecision(18, 4);
        stock.Property(x => x.Version).IsConcurrencyToken();
        stock.Ignore(x => x.Available);
        stock.HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);

        var movement = modelBuilder.Entity<InventoryMovement>();
        movement.ToTable("movement", "inventory");
        movement.HasKey(x => x.Id);
        movement.HasIndex(x => new { x.TenantId, x.CommandId }).IsUnique();
        movement.HasIndex(x => new { x.TenantId, x.StockDimensionId, x.OccurredAt, x.Id });
        movement.Property(x => x.Quantity).HasPrecision(18, 4);
        movement.HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);

        var reservation = modelBuilder.Entity<InventoryReservation>();
        reservation.ToTable("reservation", "inventory");
        reservation.HasKey(x => x.Id);
        reservation.HasIndex(x => new { x.TenantId, x.CommandId, x.StockDimensionId }).IsUnique();
        reservation.Property(x => x.Quantity).HasPrecision(18, 4);
        reservation.Property(x => x.ConsumedQuantity).HasPrecision(18, 4);
        reservation.Property(x => x.ReleasedQuantity).HasPrecision(18, 4);
        reservation.HasQueryFilter(x => tenant.TenantId == Guid.Empty || x.TenantId == tenant.TenantId);
        Wms.Tenancy.ModelNaming.Apply(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries<InventoryMovement>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Inventory movements are append-only.");
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}

public sealed record ReceiveStockCommand(Guid WarehouseId, Guid OwnerId, string Sku, Guid LocationId, string Status, decimal Quantity, string Uom, Guid CommandId, long? ExpectedVersion);
public sealed record ReserveStockCommand(Guid WarehouseId, Guid OwnerId, string Sku, Guid OrderId, Guid OrderLineId, decimal Quantity, Guid CommandId);
public sealed record TransferStockCommand(Guid WarehouseId, Guid OwnerId, string Sku, Guid SourceLocationId, Guid DestinationLocationId, string Status, decimal Quantity, string Uom, Guid CommandId);
public sealed record PickStockCommand(Guid WarehouseId, Guid OwnerId, string Sku, Guid LocationId, Guid OrderId, Guid OrderLineId, decimal Quantity, string Uom, Guid CommandId);
public sealed record ReleaseReservedStockCommand(Guid WarehouseId, Guid OwnerId, string Sku, Guid LocationId, Guid OrderId, Guid OrderLineId, decimal Quantity, string Uom, Guid CommandId);
public sealed record StockDto(Guid Id, Guid WarehouseId, Guid OwnerId, string Sku, Guid LocationId, string Status, decimal OnHand, decimal Reserved, decimal Blocked, decimal Available, long Version);

public interface IInventoryCommands
{
    Task<StockDto> ReceiveAsync(ReceiveStockCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryReservation>> ReserveFifoAsync(ReserveStockCommand command, CancellationToken cancellationToken);
    Task<StockDto> TransferAsync(TransferStockCommand command, CancellationToken cancellationToken);
    Task<StockDto> PickAsync(PickStockCommand command, CancellationToken cancellationToken);
    Task<StockDto> ReleaseReservedAsync(ReleaseReservedStockCommand command, CancellationToken cancellationToken);
}

public sealed class InventoryService(InventoryDbContext db, ITenantContext tenant) : IInventoryCommands
{
    public async Task<StockDto> ReceiveAsync(ReceiveStockCommand command, CancellationToken cancellationToken)
    {
        RequireTenant();
        var checksum = PayloadChecksum.Compute(command);
        var duplicate = await db.Movements.SingleOrDefaultAsync(x => x.CommandId == command.CommandId, cancellationToken);
        if (duplicate is not null)
        {
            if (duplicate.PayloadChecksum != checksum) throw new WmsProblemException("DUPLICATE_PAYLOAD_MISMATCH", "Command ID was reused with another payload.", 409);
            var previous = await db.StockBalances.SingleAsync(x => x.Id == duplicate.StockDimensionId, cancellationToken);
            return ToDto(previous);
        }

        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var balance = await db.StockBalances.SingleOrDefaultAsync(x =>
            x.WarehouseId == command.WarehouseId && x.OwnerId == command.OwnerId && x.Sku == command.Sku &&
            x.LocationId == command.LocationId && x.Status == command.Status, cancellationToken);

        if (balance is null)
        {
            balance = new StockBalance
            {
                TenantId = tenant.TenantId,
                CreatedBy = tenant.ActorId,
                CorrelationId = tenant.CorrelationId,
                WarehouseId = command.WarehouseId,
                OwnerId = command.OwnerId,
                Sku = command.Sku,
                LocationId = command.LocationId,
                Status = command.Status,
                ReceivedAt = DateTimeOffset.UtcNow,
                Version = 0
            };
            db.StockBalances.Add(balance);
        }

        var aggregate = new StockDimensionAggregate(balance.OnHand, balance.Reserved, balance.Blocked, balance.Version);
        aggregate.Receive(command.Quantity, command.ExpectedVersion);
        var movement = new InventoryMovement
        {
            TenantId = tenant.TenantId,
            CreatedBy = tenant.ActorId,
            CorrelationId = tenant.CorrelationId,
            StockDimensionId = balance.Id,
            MovementType = "Receipt",
            Quantity = command.Quantity,
            Uom = command.Uom,
            CommandId = command.CommandId,
            PayloadChecksum = checksum,
            OccurredAt = DateTimeOffset.UtcNow
        };
        balance.OnHand = aggregate.OnHand;
        balance.Reserved = aggregate.Reserved;
        balance.Blocked = aggregate.Blocked;
        balance.Version = aggregate.Version;
        balance.LastMovementId = movement.Id;
        db.Movements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return ToDto(balance);
    }

    public async Task<IReadOnlyList<InventoryReservation>> ReserveFifoAsync(ReserveStockCommand command, CancellationToken cancellationToken)
    {
        RequireTenant();
        if (command.Quantity <= 0) throw new WmsProblemException("VALIDATION_FAILED", "Quantity must be positive.");
        var checksum = PayloadChecksum.Compute(command);
        var prior = await db.Reservations.Where(x => x.CommandId == command.CommandId).ToListAsync(cancellationToken);
        if (prior.Count > 0)
        {
            if (prior.Any(x => x.PayloadChecksum != checksum)) throw new WmsProblemException("DUPLICATE_PAYLOAD_MISMATCH", "Command ID was reused with another payload.", 409);
            return prior;
        }

        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var candidates = await db.StockBalances
            .Where(x => x.WarehouseId == command.WarehouseId && x.OwnerId == command.OwnerId && x.Sku == command.Sku && x.Status == "Available" && x.OnHand - x.Reserved - x.Blocked > 0)
            .OrderBy(x => x.ReceivedAt).ThenBy(x => x.LastMovementId)
            .ToListAsync(cancellationToken);

        var remaining = command.Quantity;
        var created = new List<InventoryReservation>();
        foreach (var balance in candidates)
        {
            if (remaining <= 0) break;
            var quantity = Math.Min(remaining, balance.Available);
            var aggregate = new StockDimensionAggregate(balance.OnHand, balance.Reserved, balance.Blocked, balance.Version);
            aggregate.Reserve(quantity, balance.Version);
            balance.Reserved = aggregate.Reserved;
            balance.Version = aggregate.Version;
            var reservation = new InventoryReservation
            {
                TenantId = tenant.TenantId,
                CreatedBy = tenant.ActorId,
                CorrelationId = tenant.CorrelationId,
                StockDimensionId = balance.Id,
                OrderId = command.OrderId,
                OrderLineId = command.OrderLineId,
                Quantity = quantity,
                CommandId = command.CommandId,
                PayloadChecksum = checksum
            };
            created.Add(reservation);
            db.Reservations.Add(reservation);
            remaining -= quantity;
        }

        if (remaining > 0) throw new WmsProblemException("INSUFFICIENT_AVAILABLE", "Insufficient eligible stock.", 409);
        await db.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return created;
    }

    public async Task<StockDto> TransferAsync(TransferStockCommand command, CancellationToken cancellationToken)
    {
        RequireTenant();
        if (command.SourceLocationId == command.DestinationLocationId)
            throw new WmsProblemException("VALIDATION_FAILED", "Source and destination locations must differ.");
        var checksum = PayloadChecksum.Compute(command);
        var prior = await db.Movements.SingleOrDefaultAsync(x => x.CommandId == command.CommandId, cancellationToken);
        if (prior is not null)
        {
            if (prior.PayloadChecksum != checksum) throw new WmsProblemException("DUPLICATE_PAYLOAD_MISMATCH", "Command ID was reused with another payload.", 409);
            return ToDto(await db.StockBalances.SingleAsync(x => x.Id == prior.RelatedStockDimensionId, cancellationToken));
        }

        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var source = await db.StockBalances.SingleOrDefaultAsync(x => x.WarehouseId == command.WarehouseId && x.OwnerId == command.OwnerId &&
            x.Sku == command.Sku && x.LocationId == command.SourceLocationId && x.Status == command.Status, cancellationToken)
            ?? throw new WmsProblemException("STOCK_NOT_FOUND", "Source stock dimension was not found.", 404);
        var destination = await db.StockBalances.SingleOrDefaultAsync(x => x.WarehouseId == command.WarehouseId && x.OwnerId == command.OwnerId &&
            x.Sku == command.Sku && x.LocationId == command.DestinationLocationId && x.Status == command.Status, cancellationToken);
        if (destination is null)
        {
            destination = new StockBalance
            {
                TenantId = tenant.TenantId, CreatedBy = tenant.ActorId, CorrelationId = tenant.CorrelationId,
                WarehouseId = command.WarehouseId, OwnerId = command.OwnerId, Sku = command.Sku,
                LocationId = command.DestinationLocationId, Status = command.Status, ReceivedAt = source.ReceivedAt, Version = 0
            };
            db.StockBalances.Add(destination);
        }

        var sourceAggregate = new StockDimensionAggregate(source.OnHand, source.Reserved, source.Blocked, source.Version);
        var destinationAggregate = new StockDimensionAggregate(destination.OnHand, destination.Reserved, destination.Blocked, destination.Version);
        sourceAggregate.TransferOut(command.Quantity, source.Version);
        destinationAggregate.Receive(command.Quantity, destination.Version);
        var movement = NewMovement(source.Id, destination.Id, "Transfer", command.Quantity, command.Uom, command.CommandId, checksum);
        Apply(source, sourceAggregate, movement.Id);
        Apply(destination, destinationAggregate, movement.Id);
        db.Movements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return ToDto(destination);
    }

    public async Task<StockDto> PickAsync(PickStockCommand command, CancellationToken cancellationToken)
    {
        RequireTenant();
        var checksum = PayloadChecksum.Compute(command);
        var prior = await db.Movements.SingleOrDefaultAsync(x => x.CommandId == command.CommandId, cancellationToken);
        if (prior is not null)
        {
            if (prior.PayloadChecksum != checksum) throw new WmsProblemException("DUPLICATE_PAYLOAD_MISMATCH", "Command ID was reused with another payload.", 409);
            return ToDto(await db.StockBalances.SingleAsync(x => x.Id == prior.StockDimensionId, cancellationToken));
        }

        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var stock = await db.StockBalances.SingleOrDefaultAsync(x => x.WarehouseId == command.WarehouseId && x.OwnerId == command.OwnerId &&
            x.Sku == command.Sku && x.LocationId == command.LocationId && x.Status == "Available", cancellationToken)
            ?? throw new WmsProblemException("STOCK_NOT_FOUND", "Pick stock dimension was not found.", 404);
        var reservation = await db.Reservations.SingleOrDefaultAsync(x => x.StockDimensionId == stock.Id && x.OrderId == command.OrderId &&
            x.OrderLineId == command.OrderLineId && x.Status == "Active", cancellationToken)
            ?? throw new WmsProblemException("RESERVATION_NOT_ACTIVE", "No active reservation matches this pick.", 409);
        if (reservation.Quantity - reservation.ConsumedQuantity - reservation.ReleasedQuantity < command.Quantity)
            throw new WmsProblemException("RESERVATION_NOT_ACTIVE", "Pick quantity exceeds the active reservation.", 409);

        var aggregate = new StockDimensionAggregate(stock.OnHand, stock.Reserved, stock.Blocked, stock.Version);
        aggregate.Consume(command.Quantity, stock.Version);
        reservation.ConsumedQuantity += command.Quantity;
        reservation.Version++;
        if (reservation.ConsumedQuantity + reservation.ReleasedQuantity == reservation.Quantity) reservation.Status = "Consumed";
        var movement = NewMovement(stock.Id, null, "Pick", -command.Quantity, command.Uom, command.CommandId, checksum);
        Apply(stock, aggregate, movement.Id);
        db.Movements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return ToDto(stock);
    }

    public async Task<StockDto> ReleaseReservedAsync(ReleaseReservedStockCommand command, CancellationToken cancellationToken)
    {
        RequireTenant();
        var checksum = PayloadChecksum.Compute(command);
        var prior = await db.Movements.SingleOrDefaultAsync(x => x.CommandId == command.CommandId, cancellationToken);
        if (prior is not null)
        {
            if (prior.PayloadChecksum != checksum) throw new WmsProblemException("DUPLICATE_PAYLOAD_MISMATCH", "Command ID was reused with another payload.", 409);
            return ToDto(await db.StockBalances.SingleAsync(x => x.Id == prior.StockDimensionId, cancellationToken));
        }

        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var stock = await db.StockBalances.SingleOrDefaultAsync(x => x.WarehouseId == command.WarehouseId && x.OwnerId == command.OwnerId &&
            x.Sku == command.Sku && x.LocationId == command.LocationId && x.Status == "Available", cancellationToken)
            ?? throw new WmsProblemException("STOCK_NOT_FOUND", "Reserved stock dimension was not found.", 404);
        var reservation = await db.Reservations.SingleOrDefaultAsync(x => x.StockDimensionId == stock.Id && x.OrderId == command.OrderId &&
            x.OrderLineId == command.OrderLineId && x.Status == "Active", cancellationToken)
            ?? throw new WmsProblemException("RESERVATION_NOT_ACTIVE", "No active reservation matches this release.", 409);
        if (reservation.Quantity - reservation.ConsumedQuantity - reservation.ReleasedQuantity < command.Quantity)
            throw new WmsProblemException("RESERVATION_NOT_ACTIVE", "Release quantity exceeds the active reservation.", 409);

        var aggregate = new StockDimensionAggregate(stock.OnHand, stock.Reserved, stock.Blocked, stock.Version);
        aggregate.Release(command.Quantity, stock.Version);
        reservation.ReleasedQuantity += command.Quantity;
        reservation.Version++;
        if (reservation.ConsumedQuantity + reservation.ReleasedQuantity == reservation.Quantity) reservation.Status = "Consumed";
        var movement = NewMovement(stock.Id, null, "ReservationRelease", command.Quantity, command.Uom, command.CommandId, checksum);
        Apply(stock, aggregate, movement.Id);
        db.Movements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return ToDto(stock);
    }

    private void RequireTenant()
    {
        if (!tenant.IsResolved) throw new WmsProblemException("TENANT_REQUIRED", "Tenant context is required.", 401);
    }

    private InventoryMovement NewMovement(Guid stockId, Guid? relatedStockId, string type, decimal quantity, string uom, Guid commandId, string checksum) => new()
    {
        TenantId = tenant.TenantId, CreatedBy = tenant.ActorId, CorrelationId = tenant.CorrelationId,
        StockDimensionId = stockId, RelatedStockDimensionId = relatedStockId, MovementType = type,
        Quantity = quantity, Uom = uom, CommandId = commandId, PayloadChecksum = checksum, OccurredAt = DateTimeOffset.UtcNow
    };

    private static void Apply(StockBalance balance, StockDimensionAggregate aggregate, Guid movementId)
    {
        balance.OnHand = aggregate.OnHand;
        balance.Reserved = aggregate.Reserved;
        balance.Blocked = aggregate.Blocked;
        balance.Version = aggregate.Version;
        balance.LastMovementId = movementId;
    }

    public static StockDto ToDto(StockBalance x) => new(x.Id, x.WarehouseId, x.OwnerId, x.Sku, x.LocationId, x.Status, x.OnHand, x.Reserved, x.Blocked, x.Available, x.Version);
}
