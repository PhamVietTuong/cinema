using Cinema.Business.DTO.Inventory;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class StoragePlanServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly StoragePlanManager _sut;
    private readonly Dictionary<Guid, FoodAndDrink> _foods = new();

    private static readonly Guid TheaterA = Guid.NewGuid();
    private static readonly Guid TheaterB = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public StoragePlanServiceTests()
    {
        _sut = new StoragePlanManager(_uowMock.Object);
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids) => _foods.Where(f => ids.Contains(f.Key)).ToDictionary(f => f.Key, f => f.Value));
        _uowMock.Setup(u => u.UserStore.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _uowMock.Setup(u => u.TheaterStore.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>())).ReturnsAsync(true);
        _uowMock.Setup(u => u.StockMovementStore.CreateAsync(It.IsAny<StockMovement>())).ReturnsAsync((StockMovement m) => m);
        _uowMock.Setup(u => u.StoragePlanStore.CreateAsync(It.IsAny<StoragePlan>())).ReturnsAsync((StoragePlan p) => p);
        _uowMock.Setup(u => u.StoragePlanStore.UpdateAsync(It.IsAny<StoragePlan>())).ReturnsAsync((StoragePlan p) => p);
    }

    private FoodAndDrink GivenFood(bool tracked = true, bool combo = false, Guid? theaterId = null, int onHand = 0, int target = 0, string name = "Popcorn")
    {
        var food = new FoodAndDrink
        {
            Id = Guid.NewGuid(),
            TheaterId = theaterId ?? TheaterA,
            Name = name,
            TrackInventory = tracked,
            IsCombo = combo,
            QuantityOnHand = onHand,
            TargetStockLevel = target
        };
        _foods[food.Id] = food;
        return food;
    }

    private StoragePlan GivenPlan(StoragePlanStatus status, Guid? theaterId = null, params (FoodAndDrink Food, int Planned)[] lines)
    {
        var plan = new StoragePlan
        {
            Id = Guid.NewGuid(),
            Code = "SP-TEST",
            TheaterId = theaterId ?? TheaterA,
            Status = status,
            TargetDate = DateTime.UtcNow.Date,
            CreatedByUserId = UserId
        };
        foreach (var (food, planned) in lines)
        {
            plan.Items.Add(new StoragePlanItem { Id = Guid.NewGuid(), StoragePlanId = plan.Id, FoodAndDrinkId = food.Id, PlannedQuantity = planned });
        }
        _uowMock.Setup(u => u.StoragePlanStore.GetWithItemsAsync(plan.Id)).ReturnsAsync(plan);
        return plan;
    }

    private static SaveStoragePlanRequest Request(Guid? id, params (Guid FoodId, int Quantity)[] lines)
    {
        return new SaveStoragePlanRequest
        {
            Id = id,
            TheaterId = TheaterA,
            TargetDate = DateTime.UtcNow.Date,
            Items = lines.Select(l => new SaveStoragePlanItem { FoodAndDrinkId = l.FoodId, PlannedQuantity = l.Quantity }).ToList()
        };
    }

    // ── Create / validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_CreatesDraftWithGeneratedCode()
    {
        var food = GivenFood();

        var dto = await _sut.CreateAsync(Request(null, (food.Id, 5)), UserId, TheaterA);

        dto.Status.Should().Be(StoragePlanStatus.Draft);
        dto.Code.Should().MatchRegex(@"^SP\d{18}$");
        dto.Items.Should().ContainSingle().Which.PlannedQuantity.Should().Be(5);
        _uowMock.Verify(u => u.StoragePlanStore.CreateAsync(It.Is<StoragePlan>(p => p.CreatedByUserId == UserId)), Times.Once);
    }

    [Fact]
    public async Task Create_ItemFromOtherTheater_Throws()
    {
        var food = GivenFood(theaterId: TheaterB);

        await _sut.Invoking(s => s.CreateAsync(Request(null, (food.Id, 5)), UserId, null))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_UnknownItem_Throws()
    {
        await _sut.Invoking(s => s.CreateAsync(Request(null, (Guid.NewGuid(), 5)), UserId, null))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_UntrackedItem_Throws()
    {
        var food = GivenFood(tracked: false);

        await _sut.Invoking(s => s.CreateAsync(Request(null, (food.Id, 5)), UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_ComboItem_Throws()
    {
        var food = GivenFood(combo: true);

        await _sut.Invoking(s => s.CreateAsync(Request(null, (food.Id, 5)), UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_DuplicateItem_Throws()
    {
        var food = GivenFood();

        await _sut.Invoking(s => s.CreateAsync(Request(null, (food.Id, 5), (food.Id, 2)), UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_PastTargetDate_Throws()
    {
        var food = GivenFood();
        var request = Request(null, (food.Id, 5));
        request.TargetDate = DateTime.UtcNow.Date.AddDays(-1);

        await _sut.Invoking(s => s.CreateAsync(request, UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_ScopedCallerForAnotherTheater_ThrowsNotFound()
    {
        var food = GivenFood(theaterId: TheaterB);
        var request = Request(null, (food.Id, 5));
        request.TheaterId = TheaterB;

        await _sut.Invoking(s => s.CreateAsync(request, UserId, TheaterA))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_FromRejected_GoesBackToDraftAndClearsReason()
    {
        var kept = GivenFood();
        var dropped = GivenFood(name: "Cola");
        var added = GivenFood(name: "Nachos");
        var plan = GivenPlan(StoragePlanStatus.Rejected, null, (kept, 3), (dropped, 4));
        plan.RejectionReason = "Too much";

        var dto = await _sut.UpdateAsync(Request(plan.Id, (kept.Id, 9), (added.Id, 1)), UserId, TheaterA);

        dto.Status.Should().Be(StoragePlanStatus.Draft);
        plan.RejectionReason.Should().BeNull();
        plan.Items.Select(i => i.FoodAndDrinkId).Should().BeEquivalentTo(new[] { kept.Id, added.Id });
        plan.Items.Single(i => i.FoodAndDrinkId == kept.Id).PlannedQuantity.Should().Be(9);
    }

    [Theory]
    [InlineData(StoragePlanStatus.Submitted)]
    [InlineData(StoragePlanStatus.Approved)]
    [InlineData(StoragePlanStatus.Received)]
    [InlineData(StoragePlanStatus.Cancelled)]
    public async Task Update_FromOtherStatus_Throws(StoragePlanStatus status)
    {
        var food = GivenFood();
        var plan = GivenPlan(status, null, (food, 3));

        await _sut.Invoking(s => s.UpdateAsync(Request(plan.Id, (food.Id, 1)), UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // ── State machine ────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_Draft_SetsSubmittedAt()
    {
        var food = GivenFood();
        var plan = GivenPlan(StoragePlanStatus.Draft, null, (food, 3));

        var dto = await _sut.SubmitAsync(plan.Id, UserId, TheaterA);

        dto.Status.Should().Be(StoragePlanStatus.Submitted);
        plan.SubmittedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Submit_WithoutItems_Throws()
    {
        var plan = GivenPlan(StoragePlanStatus.Draft);

        await _sut.Invoking(s => s.SubmitAsync(plan.Id, UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(StoragePlanStatus.Submitted)]
    [InlineData(StoragePlanStatus.Approved)]
    [InlineData(StoragePlanStatus.Rejected)]
    [InlineData(StoragePlanStatus.Received)]
    [InlineData(StoragePlanStatus.Cancelled)]
    public async Task Submit_FromNonDraft_Throws(StoragePlanStatus status)
    {
        var food = GivenFood();
        var plan = GivenPlan(status, null, (food, 3));

        await _sut.Invoking(s => s.SubmitAsync(plan.Id, UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Approve_Submitted_SetsDecision()
    {
        var food = GivenFood();
        var plan = GivenPlan(StoragePlanStatus.Submitted, null, (food, 3));

        var dto = await _sut.ApproveAsync(plan.Id, UserId, TheaterA);

        dto.Status.Should().Be(StoragePlanStatus.Approved);
        plan.DecidedByUserId.Should().Be(UserId);
        plan.DecidedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(StoragePlanStatus.Draft)]
    [InlineData(StoragePlanStatus.Approved)]
    [InlineData(StoragePlanStatus.Rejected)]
    [InlineData(StoragePlanStatus.Received)]
    [InlineData(StoragePlanStatus.Cancelled)]
    public async Task ApproveAndReject_FromNonSubmitted_Throw(StoragePlanStatus status)
    {
        var food = GivenFood();
        var plan = GivenPlan(status, null, (food, 3));

        await _sut.Invoking(s => s.ApproveAsync(plan.Id, UserId, TheaterA)).Should().ThrowAsync<InvalidOperationException>();
        await _sut.Invoking(s => s.RejectAsync(plan.Id, "No", UserId, TheaterA)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reject_WithoutReason_Throws(string? reason)
    {
        var food = GivenFood();
        var plan = GivenPlan(StoragePlanStatus.Submitted, null, (food, 3));

        await _sut.Invoking(s => s.RejectAsync(plan.Id, reason, UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>();
        plan.Status.Should().Be(StoragePlanStatus.Submitted);
    }

    [Fact]
    public async Task Reject_WithReason_StoresIt()
    {
        var food = GivenFood();
        var plan = GivenPlan(StoragePlanStatus.Submitted, null, (food, 3));

        var dto = await _sut.RejectAsync(plan.Id, " Too much ", UserId, TheaterA);

        dto.Status.Should().Be(StoragePlanStatus.Rejected);
        dto.RejectionReason.Should().Be("Too much");
    }

    [Theory]
    [InlineData(StoragePlanStatus.Draft)]
    [InlineData(StoragePlanStatus.Submitted)]
    [InlineData(StoragePlanStatus.Approved)]
    public async Task Cancel_FromOpenStatus_Succeeds(StoragePlanStatus status)
    {
        var food = GivenFood();
        var plan = GivenPlan(status, null, (food, 3));

        var dto = await _sut.CancelAsync(plan.Id, UserId, TheaterA);

        dto.Status.Should().Be(StoragePlanStatus.Cancelled);
    }

    [Theory]
    [InlineData(StoragePlanStatus.Rejected)]
    [InlineData(StoragePlanStatus.Received)]
    [InlineData(StoragePlanStatus.Cancelled)]
    public async Task Cancel_FromClosedStatus_Throws(StoragePlanStatus status)
    {
        var food = GivenFood();
        var plan = GivenPlan(status, null, (food, 3));

        await _sut.Invoking(s => s.CancelAsync(plan.Id, UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(StoragePlanStatus.Draft)]
    [InlineData(StoragePlanStatus.Submitted)]
    [InlineData(StoragePlanStatus.Rejected)]
    [InlineData(StoragePlanStatus.Received)]
    [InlineData(StoragePlanStatus.Cancelled)]
    public async Task Receive_FromNonApproved_Throws(StoragePlanStatus status)
    {
        var food = GivenFood();
        var plan = GivenPlan(status, null, (food, 3));

        await _sut.Invoking(s => s.ReceiveAsync(new ReceiveStoragePlanRequest { Id = plan.Id }, UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>();
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    // ── Receive ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Receive_DefaultsToPlannedQuantityAndAppliesInAscendingIdOrder()
    {
        var foodA = GivenFood();
        var foodB = GivenFood(name: "Cola");
        var foodC = GivenFood(name: "Nachos");
        var plan = GivenPlan(StoragePlanStatus.Approved, null, (foodA, 5), (foodB, 7), (foodC, 9));
        var overridden = plan.Items.Single(i => i.FoodAndDrinkId == foodB.Id);

        var applied = new List<(Guid Id, int Delta)>();
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .Callback((Guid id, int delta) => applied.Add((id, delta)))
            .ReturnsAsync(true);
        var movements = new List<StockMovement>();
        _uowMock.Setup(u => u.StockMovementStore.CreateAsync(It.IsAny<StockMovement>()))
            .Callback((StockMovement m) => movements.Add(m))
            .ReturnsAsync((StockMovement m) => m);

        var request = new ReceiveStoragePlanRequest
        {
            Id = plan.Id,
            Items = new List<ReceiveStoragePlanItem>
            {
                new() { StoragePlanItemId = overridden.Id, ReceivedQuantity = 0 }
            }
        };
        var dto = await _sut.ReceiveAsync(request, UserId, TheaterA);

        dto.Status.Should().Be(StoragePlanStatus.Received);
        plan.ReceivedByUserId.Should().Be(UserId);
        plan.ReceivedAt.Should().NotBeNull();
        overridden.ReceivedQuantity.Should().Be(0);
        plan.Items.Single(i => i.FoodAndDrinkId == foodA.Id).ReceivedQuantity.Should().Be(5);

        // The item received at 0 moves nothing; the others are applied in ascending id order.
        applied.Select(a => a.Id).Should().BeInAscendingOrder();
        applied.Should().HaveCount(2);
        applied.Should().Contain((foodA.Id, 5));
        applied.Should().Contain((foodC.Id, 9));
        movements.Should().HaveCount(2);
        movements.Should().OnlyContain(m => m.Type == StockMovementType.Receive
            && m.StoragePlanId == plan.Id
            && m.TheaterId == TheaterA
            && m.UserId == UserId
            && m.ReasonCode == null
            && m.Reason == plan.Code
            && m.Quantity > 0);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task Receive_Twice_SecondThrows()
    {
        var food = GivenFood();
        var plan = GivenPlan(StoragePlanStatus.Approved, null, (food, 5));
        var request = new ReceiveStoragePlanRequest { Id = plan.Id };

        await _sut.ReceiveAsync(request, UserId, TheaterA);

        await _sut.Invoking(s => s.ReceiveAsync(request, UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>();
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(food.Id, 5), Times.Once);
    }

    [Fact]
    public async Task Receive_ConcurrencyConflict_MapsToInvalidOperationAndRollsBack()
    {
        var food = GivenFood();
        var plan = GivenPlan(StoragePlanStatus.Approved, null, (food, 5));
        _uowMock.Setup(u => u.StoragePlanStore.UpdateAsync(It.IsAny<StoragePlan>()))
            .ThrowsAsync(new ConcurrencyConflictException("conflict"));

        await _sut.Invoking(s => s.ReceiveAsync(new ReceiveStoragePlanRequest { Id = plan.Id }, UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The plan was modified by someone else, please reload.");

        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Never);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Receive_ItemNoLongerTracked_ThrowsAndRollsBack()
    {
        var food = GivenFood(name: "Popcorn");
        var plan = GivenPlan(StoragePlanStatus.Approved, null, (food, 5));
        food.TrackInventory = false;

        await _sut.Invoking(s => s.ReceiveAsync(new ReceiveStoragePlanRequest { Id = plan.Id }, UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Popcorn*");

        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Never);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        _uowMock.Verify(u => u.StockMovementStore.CreateAsync(It.IsAny<StockMovement>()), Times.Never);
        plan.Status.Should().Be(StoragePlanStatus.Approved);
    }

    // ── Scope ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OutOfScopePlan_BehavesAsNotFound_ForEveryOperation()
    {
        var food = GivenFood(theaterId: TheaterB);
        var plan = GivenPlan(StoragePlanStatus.Submitted, TheaterB, (food, 3));

        await _sut.Invoking(s => s.GetByIdAsync(plan.Id, TheaterA)).Should().ThrowAsync<KeyNotFoundException>();
        await _sut.Invoking(s => s.SubmitAsync(plan.Id, UserId, TheaterA)).Should().ThrowAsync<KeyNotFoundException>();
        await _sut.Invoking(s => s.ApproveAsync(plan.Id, UserId, TheaterA)).Should().ThrowAsync<KeyNotFoundException>();
        await _sut.Invoking(s => s.RejectAsync(plan.Id, "No", UserId, TheaterA)).Should().ThrowAsync<KeyNotFoundException>();
        await _sut.Invoking(s => s.CancelAsync(plan.Id, UserId, TheaterA)).Should().ThrowAsync<KeyNotFoundException>();
        await _sut.Invoking(s => s.ReceiveAsync(new ReceiveStoragePlanRequest { Id = plan.Id }, UserId, TheaterA)).Should().ThrowAsync<KeyNotFoundException>();
        await _sut.Invoking(s => s.UpdateAsync(Request(plan.Id, (food.Id, 1)), UserId, TheaterA)).Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AdminScope_CanReachAnyTheater()
    {
        var food = GivenFood(theaterId: TheaterB);
        var plan = GivenPlan(StoragePlanStatus.Submitted, TheaterB, (food, 3));

        var dto = await _sut.ApproveAsync(plan.Id, UserId, null);

        dto.Status.Should().Be(StoragePlanStatus.Approved);
    }

    // ── Create from low stock ────────────────────────────────────────────────

    [Fact]
    public async Task CreateFromLowStock_BuildsQuantitiesFromTargetLevel()
    {
        var needsTen = GivenFood(onHand: 2, target: 12, name: "Popcorn");
        var atTarget = GivenFood(onHand: 5, target: 5, name: "Cola");
        var noTarget = GivenFood(onHand: 0, target: 0, name: "Nachos");
        _uowMock.Setup(u => u.FoodAndDrinkStore.SearchInventoryAsync(It.Is<InventorySearchCriteria>(c =>
                c.TheaterId == TheaterA && c.LowStock == true && c.TrackedOnly)))
            .ReturnsAsync((new List<FoodAndDrink> { needsTen, atTarget, noTarget }, 3));

        var dto = await _sut.CreateFromLowStockAsync(new CreatePlanFromLowStockRequest { TheaterId = TheaterA }, UserId, TheaterA);

        dto.Status.Should().Be(StoragePlanStatus.Draft);
        dto.Items.Should().HaveCount(2);
        dto.Items.Single(i => i.FoodAndDrinkId == needsTen.Id).PlannedQuantity.Should().Be(10);
        dto.Items.Single(i => i.FoodAndDrinkId == atTarget.Id).PlannedQuantity.Should().Be(1);
        dto.Items.Should().NotContain(i => i.FoodAndDrinkId == noTarget.Id);
        _uowMock.Verify(u => u.FoodAndDrinkStore.SearchInventoryAsync(It.IsAny<InventorySearchCriteria>()), Times.Once);
    }

    [Fact]
    public async Task CreateFromLowStock_NothingQualifies_Throws()
    {
        var noTarget = GivenFood(onHand: 0, target: 0);
        _uowMock.Setup(u => u.FoodAndDrinkStore.SearchInventoryAsync(It.IsAny<InventorySearchCriteria>()))
            .ReturnsAsync((new List<FoodAndDrink> { noTarget }, 1));

        await _sut.Invoking(s => s.CreateFromLowStockAsync(new CreatePlanFromLowStockRequest { TheaterId = TheaterA }, UserId, TheaterA))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No low-stock items with a target level.");
        _uowMock.Verify(u => u.StoragePlanStore.CreateAsync(It.IsAny<StoragePlan>()), Times.Never);
    }

    [Fact]
    public async Task CreateFromLowStock_ScopedCallerForAnotherTheater_ThrowsNotFound()
    {
        await _sut.Invoking(s => s.CreateFromLowStockAsync(new CreatePlanFromLowStockRequest { TheaterId = TheaterB }, UserId, TheaterA))
            .Should().ThrowAsync<KeyNotFoundException>();
    }
}
