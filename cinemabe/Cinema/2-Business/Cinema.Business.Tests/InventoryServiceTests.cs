using Cinema.Business.DTO.Inventory;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class InventoryServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly InventoryManager _sut;

    private static readonly Guid TheaterA = Guid.NewGuid();
    private static readonly Guid TheaterB = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public InventoryServiceTests()
    {
        _sut = new InventoryManager(_uowMock.Object);
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>())).ReturnsAsync(true);
        _uowMock.Setup(u => u.StockMovementStore.CreateAsync(It.IsAny<StockMovement>()))
            .ReturnsAsync((StockMovement m) => m);
    }

    private FoodAndDrink GivenItem(bool tracked = true, int onHand = 10, bool combo = false, Guid? theaterId = null)
    {
        var item = new FoodAndDrink
        {
            Id = Guid.NewGuid(),
            TheaterId = theaterId ?? TheaterA,
            Name = "Popcorn",
            TrackInventory = tracked,
            QuantityOnHand = onHand,
            IsCombo = combo
        };
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdAsync(item.Id)).ReturnsAsync(item);
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(item.Id))))
            .ReturnsAsync(new Dictionary<Guid, FoodAndDrink> { [item.Id] = item });
        return item;
    }

    private void VerifyNoStockWrite()
    {
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        _uowMock.Verify(u => u.StockMovementStore.CreateAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    // ── DTO computation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, 5, 5, 20, true, false, 15)]
    [InlineData(true, 6, 5, 20, false, false, 14)]
    [InlineData(true, 0, 5, 20, true, true, 20)]
    [InlineData(true, 30, 5, 20, false, false, 0)]
    [InlineData(true, 3, 5, 0, true, false, 0)]
    [InlineData(false, 0, 5, 20, false, false, 0)]
    public void InventoryItemDTO_ComputesFlagsAndSuggestedReorder(
        bool tracked, int onHand, int threshold, int target, bool low, bool outOfStock, int reorder)
    {
        var dto = new InventoryItemDTO
        {
            TrackInventory = tracked,
            QuantityOnHand = onHand,
            LowStockThreshold = threshold,
            TargetStockLevel = target
        };

        dto.IsLowStock.Should().Be(low);
        dto.IsOutOfStock.Should().Be(outOfStock);
        dto.SuggestedReorderQuantity.Should().Be(reorder);
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSettings_EnablingTrackingWithoutOpeningQuantity_Throws()
    {
        var item = GivenItem(tracked: false, onHand: 0);

        var act = () => _sut.UpdateSettingsAsync(
            new UpdateInventorySettingsRequest { FoodAndDrinkId = item.Id, TrackInventory = true }, UserId, null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyNoStockWrite();
    }

    [Fact]
    public async Task UpdateSettings_EnablingTracking_AppliesOpeningQuantityAndWritesOpeningBalance()
    {
        var item = GivenItem(tracked: false, onHand: 0);
        StockMovement? written = null;
        _uowMock.Setup(u => u.StockMovementStore.CreateAsync(It.IsAny<StockMovement>()))
            .Callback((StockMovement m) => written = m)
            .ReturnsAsync((StockMovement m) => m);

        await _sut.UpdateSettingsAsync(new UpdateInventorySettingsRequest
        {
            FoodAndDrinkId = item.Id,
            TrackInventory = true,
            LowStockThreshold = 3,
            TargetStockLevel = 20,
            OpeningQuantity = 12
        }, UserId, null);

        item.TrackInventory.Should().BeTrue();
        item.LowStockThreshold.Should().Be(3);
        item.TargetStockLevel.Should().Be(20);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(item.Id, 12), Times.Once);
        written.Should().NotBeNull();
        written!.Type.Should().Be(StockMovementType.Adjust);
        written.ReasonCode.Should().Be(StockReasonCode.OpeningBalance);
        written.Quantity.Should().Be(12);
        written.UserId.Should().Be(UserId);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateSettings_Combo_Throws()
    {
        var item = GivenItem(tracked: false, onHand: 0, combo: true);

        var act = () => _sut.UpdateSettingsAsync(
            new UpdateInventorySettingsRequest { FoodAndDrinkId = item.Id, TrackInventory = true, OpeningQuantity = 5 }, UserId, null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyNoStockWrite();
    }

    [Fact]
    public async Task UpdateSettings_TurningTrackingOff_KeepsQuantityAndWritesNoMovement()
    {
        var item = GivenItem(tracked: true, onHand: 7);

        await _sut.UpdateSettingsAsync(
            new UpdateInventorySettingsRequest { FoodAndDrinkId = item.Id, TrackInventory = false }, UserId, null);

        item.TrackInventory.Should().BeFalse();
        item.QuantityOnHand.Should().Be(7);
        VerifyNoStockWrite();
    }

    // ── Movements ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordMovement_WasteLargerThanOnHand_Throws_AndRollsBack()
    {
        var item = GivenItem(onHand: 3);
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(item.Id, -5)).ReturnsAsync(false);

        var act = () => _sut.RecordMovementAsync(new RecordStockMovementRequest
        {
            FoodAndDrinkId = item.Id,
            Type = StockMovementType.Waste,
            Quantity = 5,
            ReasonCode = StockReasonCode.Expired
        }, UserId, null);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Stock cannot go below zero.");
        _uowMock.Verify(u => u.StockMovementStore.CreateAsync(It.IsAny<StockMovement>()), Times.Never);
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task RecordMovement_Waste_StoresNegativeQuantity()
    {
        var item = GivenItem(onHand: 10);
        StockMovement? written = null;
        _uowMock.Setup(u => u.StockMovementStore.CreateAsync(It.IsAny<StockMovement>()))
            .Callback((StockMovement m) => written = m)
            .ReturnsAsync((StockMovement m) => m);

        await _sut.RecordMovementAsync(new RecordStockMovementRequest
        {
            FoodAndDrinkId = item.Id,
            Type = StockMovementType.Waste,
            Quantity = 4,
            ReasonCode = StockReasonCode.Spilled
        }, UserId, null);

        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(item.Id, -4), Times.Once);
        written!.Quantity.Should().Be(-4);
        written.Type.Should().Be(StockMovementType.Waste);
        written.TheaterId.Should().Be(TheaterA);
    }

    [Fact]
    public async Task RecordMovement_AdjustOnUntrackedItem_Throws()
    {
        var item = GivenItem(tracked: false);

        var act = () => _sut.RecordMovementAsync(new RecordStockMovementRequest
        {
            FoodAndDrinkId = item.Id,
            Type = StockMovementType.Adjust,
            Quantity = 2,
            ReasonCode = StockReasonCode.Damaged
        }, UserId, null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyNoStockWrite();
    }

    [Fact]
    public async Task RecordMovement_OpeningBalanceReason_IsRejected()
    {
        var item = GivenItem();

        var act = () => _sut.RecordMovementAsync(new RecordStockMovementRequest
        {
            FoodAndDrinkId = item.Id,
            Type = StockMovementType.Adjust,
            Quantity = 2,
            ReasonCode = StockReasonCode.OpeningBalance
        }, UserId, null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyNoStockWrite();
    }

    [Fact]
    public async Task RecordMovement_OtherWithoutNote_IsRejected()
    {
        var item = GivenItem();

        var act = () => _sut.RecordMovementAsync(new RecordStockMovementRequest
        {
            FoodAndDrinkId = item.Id,
            Type = StockMovementType.Adjust,
            Quantity = 2,
            ReasonCode = StockReasonCode.Other,
            Note = "  "
        }, UserId, null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyNoStockWrite();
    }

    [Fact]
    public async Task RecordMovement_NonManualType_IsRejected()
    {
        var item = GivenItem();

        var act = () => _sut.RecordMovementAsync(new RecordStockMovementRequest
        {
            FoodAndDrinkId = item.Id,
            Type = StockMovementType.Sale,
            Quantity = -1,
            ReasonCode = StockReasonCode.Damaged
        }, UserId, null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyNoStockWrite();
    }

    [Fact]
    public async Task RecordMovement_Combo_Throws()
    {
        var item = GivenItem(combo: true);

        var act = () => _sut.RecordMovementAsync(new RecordStockMovementRequest
        {
            FoodAndDrinkId = item.Id,
            Type = StockMovementType.Adjust,
            Quantity = 1,
            ReasonCode = StockReasonCode.Damaged
        }, UserId, null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyNoStockWrite();
    }

    // ── Stock count ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordStockCount_WritesTheDifferenceAsStockCountCorrection()
    {
        var item = GivenItem(onHand: 10);
        StockMovement? written = null;
        _uowMock.Setup(u => u.StockMovementStore.CreateAsync(It.IsAny<StockMovement>()))
            .Callback((StockMovement m) => written = m)
            .ReturnsAsync((StockMovement m) => m);

        await _sut.RecordStockCountAsync(
            new RecordStockCountRequest { FoodAndDrinkId = item.Id, CountedQuantity = 7, Note = "Monthly count" }, UserId, null);

        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(item.Id, -3), Times.Once);
        written!.Type.Should().Be(StockMovementType.Adjust);
        written.ReasonCode.Should().Be(StockReasonCode.StockCountCorrection);
        written.Quantity.Should().Be(-3);
        written.Reason.Should().Be("Monthly count");
    }

    [Fact]
    public async Task RecordStockCount_WhenEqual_IsNoOp()
    {
        var item = GivenItem(onHand: 10);

        var result = await _sut.RecordStockCountAsync(
            new RecordStockCountRequest { FoodAndDrinkId = item.Id, CountedQuantity = 10 }, UserId, null);

        result.QuantityOnHand.Should().Be(10);
        VerifyNoStockWrite();
        _uowMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    // ── Theater scope ────────────────────────────────────────────────────────

    [Fact]
    public async Task OutOfScopeItem_BehavesAsNotFound_ForEveryWrite()
    {
        var item = GivenItem(theaterId: TheaterB);

        var settings = () => _sut.UpdateSettingsAsync(
            new UpdateInventorySettingsRequest { FoodAndDrinkId = item.Id, TrackInventory = true, OpeningQuantity = 1 }, UserId, TheaterA);
        var movement = () => _sut.RecordMovementAsync(new RecordStockMovementRequest
        {
            FoodAndDrinkId = item.Id,
            Type = StockMovementType.Adjust,
            Quantity = 1,
            ReasonCode = StockReasonCode.Damaged
        }, UserId, TheaterA);
        var count = () => _sut.RecordStockCountAsync(
            new RecordStockCountRequest { FoodAndDrinkId = item.Id, CountedQuantity = 1 }, UserId, TheaterA);

        await settings.Should().ThrowAsync<KeyNotFoundException>();
        await movement.Should().ThrowAsync<KeyNotFoundException>();
        await count.Should().ThrowAsync<KeyNotFoundException>();
        VerifyNoStockWrite();
    }

    [Fact]
    public async Task UnknownItem_BehavesAsNotFound()
    {
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, FoodAndDrink>());

        var act = () => _sut.RecordStockCountAsync(
            new RecordStockCountRequest { FoodAndDrinkId = Guid.NewGuid(), CountedQuantity = 1 }, UserId, null);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
