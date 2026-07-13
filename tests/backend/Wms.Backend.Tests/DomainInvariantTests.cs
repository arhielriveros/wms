using System.Text.Json;
using FluentAssertions;
using Wms.BuildingBlocks;
using Wms.Integration;
using Wms.Inbound;
using Wms.Inventory;
using Wms.Outbound;
using Wms.TaskExecution;
using Xunit;

namespace Wms.Backend.Tests;

public sealed class DomainInvariantTests
{
    [Fact]
    public void InventoryNeverReservesMoreThanAvailable()
    {
        var stock = new StockDimensionAggregate(onHand: 10, blocked: 2);

        var action = () => stock.Reserve(9, expectedVersion: 0);

        action.Should().Throw<WmsProblemException>()
            .Which.Code.Should().Be("INSUFFICIENT_AVAILABLE");
        stock.Available.Should().Be(8);
        stock.Reserved.Should().Be(0);
    }

    [Fact]
    public void InventoryConsumesOnlyReservedStockAndAdvancesVersion()
    {
        var stock = new StockDimensionAggregate(onHand: 10);

        stock.Reserve(4, expectedVersion: 0);
        stock.Consume(3, expectedVersion: 1);

        stock.OnHand.Should().Be(7);
        stock.Reserved.Should().Be(1);
        stock.Available.Should().Be(6);
        stock.Version.Should().Be(2);
    }

    [Fact]
    public void InventoryRejectsStaleOfflineVersionWithoutMutatingBalance()
    {
        var stock = new StockDimensionAggregate(onHand: 5, version: 7);

        var action = () => stock.Receive(2, expectedVersion: 6);

        action.Should().Throw<WmsProblemException>()
            .Which.Code.Should().Be("STALE_STOCK_VERSION");
        stock.OnHand.Should().Be(5);
        stock.Version.Should().Be(7);
    }

    [Fact]
    public void TaskFollowsAssignedStartedCompletedStateMachine()
    {
        var task = new WarehouseTaskAggregate();

        task.Assign(expectedVersion: 1);
        task.Start(expectedVersion: 2, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow);
        task.Complete(expectedVersion: 3, ownerEffectConfirmed: true);

        task.Status.Should().Be(WarehouseTaskStatus.Completed);
        task.Version.Should().Be(4);
    }

    [Fact]
    public void TaskCannotCompleteBeforeOwnerConfirmsPhysicalEffect()
    {
        var task = new WarehouseTaskAggregate(WarehouseTaskStatus.InProgress, version: 3);

        var action = () => task.Complete(expectedVersion: 3, ownerEffectConfirmed: false);

        action.Should().Throw<WmsProblemException>()
            .Which.Code.Should().Be("OWNER_EFFECT_NOT_CONFIRMED");
        task.Status.Should().Be(WarehouseTaskStatus.InProgress);
    }

    [Fact]
    public void CanonicalChecksumIsIndependentOfJsonPropertyOrder()
    {
        using var first = JsonDocument.Parse("{\"sku\":\"A-1\",\"quantity\":4}");
        using var second = JsonDocument.Parse("{\"quantity\":4,\"sku\":\"A-1\"}");

        PayloadChecksum.Compute(first.RootElement)
            .Should().Be(PayloadChecksum.Compute(second.RootElement));
    }

    [Fact]
    public void RetryScheduleAppliesBoundedExponentialBackoff()
    {
        var now = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

        var first = RetrySchedule.Next(1, now);
        var exhausted = RetrySchedule.Next(99, now);

        first.Should().BeAfter(now.AddMinutes(1)).And.BeBefore(now.AddMinutes(1).AddSeconds(21));
        exhausted.Should().BeAfter(now.AddHours(6)).And.BeBefore(now.AddHours(6).AddSeconds(21));
    }

    [Fact]
    public void TransferOutCannotMoveReservedOrBlockedStock()
    {
        var stock = new StockDimensionAggregate(onHand: 10, reserved: 4, blocked: 2, version: 3);

        var action = () => stock.TransferOut(5, expectedVersion: 3);

        action.Should().Throw<WmsProblemException>().Which.Code.Should().Be("INSUFFICIENT_AVAILABLE");
        stock.OnHand.Should().Be(10);
        stock.Version.Should().Be(3);
    }

    [Fact]
    public void InboundLifecycleClosesOnlyAfterPutaway()
    {
        var asn = new AdvanceShippingNoticeAggregate();

        asn.ReleaseReceiving(1);
        asn.MarkReceived(2);
        asn.ReleasePutaway(3);
        asn.Complete(4, hasOpenTasks: false, quantitiesClosed: true);

        asn.Status.Should().Be(AsnStatus.Completed);
        asn.Version.Should().Be(5);
    }

    [Fact]
    public void OutboundLifecycleRequiresOnlinePackingAndDispatch()
    {
        var order = new SalesOrderAggregate();
        order.Allocate(1);
        order.Release(2);
        order.StartPicking(3);

        var offline = () => order.Pack(4, online: false);
        offline.Should().Throw<WmsProblemException>().Which.Code.Should().Be("ONLINE_REQUIRED");

        order.Pack(4, online: true);
        order.Dispatch(5, online: true);
        order.Status.Should().Be(OrderStatus.Shipped);
        order.Version.Should().Be(6);
    }

    [Fact]
    public void MobileProtocolKeepsAllSevenTerminalResults()
    {
        Enum.GetValues<MobileCommandStatus>().Should().HaveCount(7)
            .And.Contain([MobileCommandStatus.Accepted, MobileCommandStatus.Rejected, MobileCommandStatus.Conflict,
                MobileCommandStatus.AlreadyProcessed, MobileCommandStatus.RequiresReview, MobileCommandStatus.Expired,
                MobileCommandStatus.Unauthorized]);
    }
}
