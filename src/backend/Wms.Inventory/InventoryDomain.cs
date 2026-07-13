using Wms.BuildingBlocks;

namespace Wms.Inventory;

public sealed class StockDimensionAggregate
{
    public decimal OnHand { get; private set; }
    public decimal Reserved { get; private set; }
    public decimal Blocked { get; private set; }
    public long Version { get; private set; }
    public decimal Available => OnHand - Reserved - Blocked;

    public StockDimensionAggregate(decimal onHand = 0, decimal reserved = 0, decimal blocked = 0, long version = 0)
    {
        if (onHand < 0 || reserved < 0 || blocked < 0 || reserved + blocked > onHand)
            throw new WmsProblemException("INVALID_STOCK_STATE", "Stock quantities violate the inventory invariant.", 409);
        OnHand = onHand;
        Reserved = reserved;
        Blocked = blocked;
        Version = version;
    }

    public void Receive(decimal quantity, long? expectedVersion = null)
    {
        ValidateQuantity(quantity);
        ValidateVersion(expectedVersion);
        OnHand += quantity;
        Version++;
    }

    public void Reserve(decimal quantity, long? expectedVersion = null)
    {
        ValidateQuantity(quantity);
        ValidateVersion(expectedVersion);
        if (quantity > Available) throw new WmsProblemException("INSUFFICIENT_AVAILABLE", "Insufficient eligible stock.", 409);
        Reserved += quantity;
        Version++;
    }

    public void Consume(decimal quantity, long? expectedVersion = null)
    {
        ValidateQuantity(quantity);
        ValidateVersion(expectedVersion);
        if (quantity > Reserved) throw new WmsProblemException("RESERVATION_NOT_ACTIVE", "The requested quantity is not reserved.", 409);
        Reserved -= quantity;
        OnHand -= quantity;
        Version++;
    }

    public void TransferOut(decimal quantity, long? expectedVersion = null)
    {
        ValidateQuantity(quantity);
        ValidateVersion(expectedVersion);
        if (quantity > Available) throw new WmsProblemException("INSUFFICIENT_AVAILABLE", "Insufficient movable stock.", 409);
        OnHand -= quantity;
        Version++;
    }

    public void Release(decimal quantity, long? expectedVersion = null)
    {
        ValidateQuantity(quantity);
        ValidateVersion(expectedVersion);
        if (quantity > Reserved) throw new WmsProblemException("RESERVATION_NOT_ACTIVE", "The requested quantity is not reserved.", 409);
        Reserved -= quantity;
        Version++;
    }

    private void ValidateVersion(long? expectedVersion)
    {
        if (expectedVersion.HasValue && expectedVersion != Version)
            throw new WmsProblemException("STALE_STOCK_VERSION", "The stock version has changed.", 409);
    }

    private static void ValidateQuantity(decimal quantity)
    {
        if (quantity <= 0) throw new WmsProblemException("VALIDATION_FAILED", "Quantity must be positive.");
    }
}
