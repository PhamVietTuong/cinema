using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Payments;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Business.DTO.Concession;
using Cinema.Business.Managers;
using Cinema.Business.Notifications;
using Cinema.Business.Payments;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class ConcessionTests
{
    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTime utcNow)
        {
            _now = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
        }

        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private readonly Mock<IApplicationUnitOfWork> _uowMock = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IStaffNotificationService> _staffNotifications = new();
    private readonly Guid _theaterId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 10, 4, 18, 0, 0);
    private readonly FoodAndDrink _cola;
    private readonly List<Invoice> _created = new();

    public ConcessionTests()
    {
        _cola = new FoodAndDrink
        {
            Id = Guid.NewGuid(), TheaterId = _theaterId, Name = "Cola", Price = 30000,
            TrackInventory = true, QuantityOnHand = 10, LowStockThreshold = 5, IsAvailable = true
        };
        _uowMock.Setup(u => u.InvoiceStore.CreateAsync(It.IsAny<Invoice>()))
            .Callback<Invoice>(i => _created.Add(i))
            .ReturnsAsync((Invoice i) => i);
        _uowMock.Setup(u => u.StockMovementStore.CreateRangeAsync(It.IsAny<List<StockMovement>>()))
            .ReturnsAsync((List<StockMovement> m) => m);
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids) => new[] { _cola }.Where(f => ids.Contains(f.Id)).ToDictionary(f => f.Id));
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>())).ReturnsAsync(true);
        _uowMock.Setup(u => u.ComboItemStore.GetByCombosAsync(It.IsAny<IReadOnlyCollection<Guid>>())).ReturnsAsync(new List<ComboItem>());
        _uowMock.Setup(u => u.DiscountStore.GetActiveAutoApplyAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<Discount>());
        _uowMock.Setup(u => u.HolidayStore.FindAsync(It.IsAny<Expression<Func<Holiday, bool>>>())).ReturnsAsync(new List<Holiday>());
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(It.IsAny<Expression<Func<MemberShip, bool>>>())).ReturnsAsync(new List<MemberShip>());
    }

    private ConcessionManager Concessions()
    {
        return new ConcessionManager(_uowMock.Object, _staffNotifications.Object, new FixedClock(_now));
    }

    private BookingManager Booking()
    {
        var gateways = new PaymentGatewayResolver(new IPaymentGateway[] { new SandboxPaymentGateway() }, "Sandbox");
        return new BookingManager(_uowMock.Object, gateways, new DevLogNotificationService(), new DevLogSmsNotificationService(),
            Mock.Of<ISeatNotificationService>(), _staffNotifications.Object);
    }

    private Task<CounterSaleResultDTO> SellColaAtCounter(int quantity, bool holdForPickup = false)
    {
        var request = new CounterSaleRequest
        {
            Foods = new List<CounterFoodItem> { new() { FoodAndDrinkId = _cola.Id, Quantity = quantity } },
            Tenders = new List<TenderLine> { new() { Method = PaymentTender.Card, Amount = 30000 * quantity, Reference = "TERM-1" } },
            HoldForPickup = holdForPickup
        };
        return Booking().SellAtCounterAsync(new CounterSaleContext { TheaterId = _theaterId, StaffUserId = _staffId, Request = request });
    }

    private PickupOrderRow Order(string code, DateTime? showTime, DateTime paidAt, FoodOrderStatus status = FoodOrderStatus.Pending)
    {
        return new PickupOrderRow
        {
            InvoiceId = Guid.NewGuid(), TheaterId = _theaterId, InvoiceCode = code, InvoiceStatus = InvoiceStatus.Paid,
            FoodStatus = status, PaidAt = paidAt, ShowTimeStart = showTime,
            Items = new List<PickupItemRow> { new() { Name = "Combo 1", Quantity = 1 } }
        };
    }

    // ── Queue ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPickupQueue_ListsPaidOnlineComboOrdersByShowtime_FoodOnlyLast_AsOneQuery()
    {
        var late = Order("LATE", _now.AddHours(3), _now.AddMinutes(-5));
        var early = Order("EARLY", _now.AddHours(1), _now.AddMinutes(-1));
        var foodOnly = Order("WALKIN", null, _now.AddMinutes(-30));
        _uowMock.Setup(u => u.InvoiceStore.GetPickupQueueAsync(_theaterId, _now.Date, _now.Date.AddDays(1)))
            .ReturnsAsync(new List<PickupOrderRow> { late, foodOnly, early });

        var queue = await Concessions().GetPickupQueueAsync(_theaterId, null);

        queue.Select(o => o.InvoiceCode).Should().Equal("EARLY", "LATE", "WALKIN");
        queue[0].Items.Should().ContainSingle(i => i.Name == "Combo 1" && i.Quantity == 1);
        _uowMock.Verify(u => u.InvoiceStore.GetPickupQueueAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    // ── Status transitions ───────────────────────────────────────────────────────

    private FoodOrderHeaderRow GivenHeader(FoodOrderStatus status, InvoiceStatus invoiceStatus = InvoiceStatus.Paid, Guid? theaterId = null)
    {
        var header = new FoodOrderHeaderRow
        {
            InvoiceId = Guid.NewGuid(), TheaterId = theaterId ?? _theaterId, InvoiceCode = "CIN1",
            InvoiceStatus = invoiceStatus, FoodStatus = status
        };
        _uowMock.Setup(u => u.InvoiceStore.GetFoodOrderHeaderAsync(header.InvoiceId)).ReturnsAsync(header);
        _uowMock.Setup(u => u.InvoiceStore.TrySetFoodStatusAsync(header.InvoiceId, It.IsAny<FoodOrderStatus>(), It.IsAny<FoodOrderStatus>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(true);
        return header;
    }

    [Fact]
    public async Task SetFoodStatus_ReadyToHandedOver_RecordsTheStaffMemberAndTheTime_AndPushesAnUpdate()
    {
        var header = GivenHeader(FoodOrderStatus.Ready);
        _uowMock.Setup(u => u.InvoiceStore.GetPickupOrderByIdAsync(header.InvoiceId)).ReturnsAsync(new PickupOrderRow
        {
            InvoiceId = header.InvoiceId, TheaterId = _theaterId, InvoiceCode = "CIN1", InvoiceStatus = InvoiceStatus.Paid,
            FoodStatus = FoodOrderStatus.HandedOver, FoodHandedOverAt = _now
        });

        var result = await Concessions().SetFoodStatusAsync(_theaterId, _staffId, header.InvoiceId, FoodOrderStatus.HandedOver);

        _uowMock.Verify(u => u.InvoiceStore.TrySetFoodStatusAsync(header.InvoiceId, FoodOrderStatus.Ready, FoodOrderStatus.HandedOver, _staffId, _now), Times.Once);
        result.FoodStatus.Should().Be(FoodOrderStatus.HandedOver);
        result.FoodHandedOverAt.Should().Be(_now);
        _staffNotifications.Verify(n => n.NotifyFoodOrderUpdatedAsync(_theaterId,
            It.Is<FoodOrderUpdateDTO>(d => d.InvoiceId == header.InvoiceId && d.FoodStatus == FoodOrderStatus.HandedOver)), Times.Once);
    }

    [Theory]
    [InlineData(FoodOrderStatus.Pending, FoodOrderStatus.HandedOver)]
    [InlineData(FoodOrderStatus.Pending, FoodOrderStatus.Ready)]
    [InlineData(FoodOrderStatus.Preparing, FoodOrderStatus.HandedOver)]
    [InlineData(FoodOrderStatus.Ready, FoodOrderStatus.Preparing)]
    [InlineData(FoodOrderStatus.HandedOver, FoodOrderStatus.Ready)]
    [InlineData(FoodOrderStatus.Cancelled, FoodOrderStatus.Preparing)]
    [InlineData(FoodOrderStatus.Pending, FoodOrderStatus.Cancelled)]
    public async Task SetFoodStatus_IllegalTransition_IsRejectedWithoutWriting(FoodOrderStatus from, FoodOrderStatus to)
    {
        var header = GivenHeader(from);

        await FluentActions.Awaiting(() => Concessions().SetFoodStatusAsync(_theaterId, _staffId, header.InvoiceId, to))
            .Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.InvoiceStore.TrySetFoodStatusAsync(It.IsAny<Guid>(), It.IsAny<FoodOrderStatus>(), It.IsAny<FoodOrderStatus>(), It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
        _staffNotifications.Verify(n => n.NotifyFoodOrderUpdatedAsync(It.IsAny<Guid>(), It.IsAny<FoodOrderUpdateDTO>()), Times.Never);
    }

    [Fact]
    public async Task SetFoodStatus_UnpaidInvoice_IsRejected()
    {
        var header = GivenHeader(FoodOrderStatus.Pending, InvoiceStatus.Pending);

        await FluentActions.Awaiting(() => Concessions().SetFoodStatusAsync(_theaterId, _staffId, header.InvoiceId, FoodOrderStatus.Preparing))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*paid*");
    }

    [Fact]
    public async Task SetFoodStatus_OtherTheatersOrder_IsNotFound()
    {
        var header = GivenHeader(FoodOrderStatus.Pending, theaterId: Guid.NewGuid());

        await FluentActions.Awaiting(() => Concessions().SetFoodStatusAsync(_theaterId, _staffId, header.InvoiceId, FoodOrderStatus.Preparing))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SetFoodStatus_LostTheRace_IsRejected()
    {
        var header = GivenHeader(FoodOrderStatus.Pending);
        _uowMock.Setup(u => u.InvoiceStore.TrySetFoodStatusAsync(header.InvoiceId, It.IsAny<FoodOrderStatus>(), It.IsAny<FoodOrderStatus>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        await FluentActions.Awaiting(() => Concessions().SetFoodStatusAsync(_theaterId, _staffId, header.InvoiceId, FoodOrderStatus.Preparing))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*someone else*");
    }

    // ── Lookup and low stock ─────────────────────────────────────────────────────

    [Fact]
    public async Task LookupPickup_UnknownCode_IsNotFound_AndKnownCodeReturnsTheOrder()
    {
        _uowMock.Setup(u => u.InvoiceStore.GetPickupOrderByCodeAsync(_theaterId, "CIN9")).ReturnsAsync(Order("CIN9", _now, _now));

        (await Concessions().LookupPickupAsync(_theaterId, " CIN9 ")).InvoiceCode.Should().Be("CIN9");
        await FluentActions.Awaiting(() => Concessions().LookupPickupAsync(_theaterId, "NOPE"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetLowStock_ProjectsTheStoreRows()
    {
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetLowStockAsync(_theaterId)).ReturnsAsync(new List<LowStockRow>
        {
            new() { FoodAndDrinkId = _cola.Id, Name = "Cola", QuantityOnHand = 2, LowStockThreshold = 5, TargetStockLevel = 40 }
        });

        var items = await Concessions().GetLowStockAsync(_theaterId);

        items.Should().ContainSingle(i => i.FoodAndDrinkId == _cola.Id && i.QuantityOnHand == 2 && i.LowStockThreshold == 5);
    }

    // ── Stamping at sale time ────────────────────────────────────────────────────

    [Fact]
    public async Task CounterSale_WithFood_IsHandedOverByDefault_AndNeverEntersTheQueue()
    {
        await SellColaAtCounter(1);

        var invoice = _created.Should().ContainSingle().Subject;
        invoice.FoodStatus.Should().Be(FoodOrderStatus.HandedOver);
        invoice.FoodHandedOverByUserId.Should().Be(_staffId);
        invoice.FoodHandedOverAt.Should().Be(invoice.PaidAt);
        _staffNotifications.Verify(n => n.NotifyFoodOrderQueuedAsync(It.IsAny<Guid>(), It.IsAny<PickupOrderDTO>()), Times.Never);
    }

    [Fact]
    public async Task CounterSale_HoldForPickup_StaysPending_AndIsPushedToTheQueue()
    {
        var orderId = Guid.Empty;
        _uowMock.Setup(u => u.InvoiceStore.GetPickupOrderByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) =>
        {
            orderId = id;
            return Order("HELD", null, _now);
        });

        await SellColaAtCounter(1, holdForPickup: true);

        var invoice = _created.Single();
        invoice.FoodStatus.Should().Be(FoodOrderStatus.Pending);
        invoice.FoodHandedOverAt.Should().BeNull();
        orderId.Should().Be(invoice.Id);
        _staffNotifications.Verify(n => n.NotifyFoodOrderQueuedAsync(_theaterId, It.Is<PickupOrderDTO>(o => o.InvoiceCode == "HELD")), Times.Once);
    }

    [Fact]
    public async Task OnlinePayment_WithFood_PushesFoodOrderQueued_AndWithoutFoodDoesNot()
    {
        var withFood = new Invoice { Id = Guid.NewGuid(), Code = "CINF", Status = InvoiceStatus.Pending, FinalAmount = 60000, PaymentMethod = "Sandbox", TheaterId = _theaterId, FoodStatus = FoodOrderStatus.Pending };
        var noFood = new Invoice { Id = Guid.NewGuid(), Code = "CINN", Status = InvoiceStatus.Pending, FinalAmount = 60000, PaymentMethod = "Sandbox", TheaterId = _theaterId, FoodStatus = FoodOrderStatus.None };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(withFood.Id)).ReturnsAsync(withFood);
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(noFood.Id)).ReturnsAsync(noFood);
        _uowMock.Setup(u => u.InvoiceStore.GetPickupOrderByIdAsync(withFood.Id)).ReturnsAsync(Order("CINF", _now.AddHours(1), _now));

        withFood.UserId = Guid.NewGuid();
        noFood.UserId = Guid.NewGuid();

        (await Booking().ConfirmPaymentAsync(withFood.UserId!.Value, withFood.Id, "REF")).Should().BeTrue();
        (await Booking().ConfirmPaymentAsync(noFood.UserId!.Value, noFood.Id, "REF")).Should().BeTrue();

        _staffNotifications.Verify(n => n.NotifyFoodOrderQueuedAsync(_theaterId, It.Is<PickupOrderDTO>(o => o.InvoiceCode == "CINF")), Times.Once);
        _staffNotifications.Verify(n => n.NotifyFoodOrderQueuedAsync(It.IsAny<Guid>(), It.IsAny<PickupOrderDTO>()), Times.Once);
        withFood.FoodStatus.Should().Be(FoodOrderStatus.Pending, "payment queues the order, it does not hand it over");
    }

    [Fact]
    public async Task CancellingAnUnpaidInvoice_CancelsItsFood_ButAFoodlessOneStaysNone()
    {
        var userId = Guid.NewGuid();
        var withFood = new Invoice { Id = Guid.NewGuid(), UserId = userId, Status = InvoiceStatus.Pending, FoodStatus = FoodOrderStatus.Pending };
        var noFood = new Invoice { Id = Guid.NewGuid(), UserId = userId, Status = InvoiceStatus.Pending, FoodStatus = FoodOrderStatus.None };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(withFood.Id)).ReturnsAsync(withFood);
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(noFood.Id)).ReturnsAsync(noFood);

        await Booking().CancelBookingAsync(userId, withFood.Id);
        await Booking().CancelBookingAsync(userId, noFood.Id);

        withFood.FoodStatus.Should().Be(FoodOrderStatus.Cancelled);
        noFood.FoodStatus.Should().Be(FoodOrderStatus.None);
    }

    // ── StockLow ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Selling_PastTheThreshold_RaisesStockLowOnce_AndOnlyForTheCrossing()
    {
        // 10 on hand, threshold 5: selling 4 leaves 6 (still above), selling 2 more crosses (6 -> 4).
        await SellColaAtCounter(4);
        _staffNotifications.Verify(n => n.NotifyStockLowAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<LowStockItemDTO>>()), Times.Never);

        _cola.QuantityOnHand = 6;
        await SellColaAtCounter(2);
        _staffNotifications.Verify(n => n.NotifyStockLowAsync(_theaterId,
            It.Is<IReadOnlyList<LowStockItemDTO>>(l => l.Count == 1 && l[0].FoodAndDrinkId == _cola.Id && l[0].QuantityOnHand == 4 && l[0].LowStockThreshold == 5)), Times.Once);

        // Already at or under the threshold: a further sale does not repeat the alert.
        _cola.QuantityOnHand = 4;
        await SellColaAtCounter(1);
        _staffNotifications.Verify(n => n.NotifyStockLowAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<LowStockItemDTO>>()), Times.Once);
    }

    [Fact]
    public async Task AFailingPush_NeverFailsTheCommittedSale()
    {
        _cola.QuantityOnHand = 6;
        _staffNotifications.Setup(n => n.NotifyStockLowAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<LowStockItemDTO>>()))
            .ThrowsAsync(new InvalidOperationException("hub down"));

        var result = await SellColaAtCounter(2);

        result.InvoiceCode.Should().NotBeNullOrEmpty();
        _created.Should().ContainSingle();
    }
}
