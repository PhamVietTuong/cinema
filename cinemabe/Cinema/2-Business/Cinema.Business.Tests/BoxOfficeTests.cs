using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.Contracts.Payments;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Managers;
using Cinema.Business.Payments;
using Cinema.Business.Security;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class BoxOfficeTests
{
    private const string _pin = "4321";

    private readonly Mock<IApplicationUnitOfWork> _uowMock = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<ISmsNotificationService> _sms = new();
    private readonly Mock<ISeatNotificationService> _seatNotifications = new();
    private readonly BookingManager _booking;
    private readonly BoxOfficeManager _sut;

    private readonly Guid _theaterId = Guid.NewGuid();
    private readonly Guid _roomTypeId = Guid.NewGuid();
    private readonly Guid _showTimeId = Guid.NewGuid();
    private readonly Guid _roomId = Guid.NewGuid();
    private readonly Guid _standardSeatType = Guid.NewGuid();
    private readonly Guid _adultId = Guid.NewGuid();
    private readonly Guid _seatA = Guid.NewGuid();
    private readonly Guid _seatB = Guid.NewGuid();

    private readonly User _staff;
    private readonly User _manager;
    private readonly CashDrawerSession _drawer;
    private readonly FoodAndDrink _cola;

    private readonly List<Invoice> _created = new();
    private readonly List<CashMovement> _cashMovements = new();
    private readonly List<AuditLog> _audits = new();
    private readonly List<StockMovement> _stockMovements = new();

    public BoxOfficeTests()
    {
        var gateways = new PaymentGatewayResolver(new IPaymentGateway[] { new SandboxPaymentGateway() }, "Sandbox");
        _booking = new BookingManager(_uowMock.Object, gateways, _notifications.Object, _sms.Object, _seatNotifications.Object);
        var audit = new AuditLogger(_uowMock.Object);
        var overrides = new ManagerOverrideService(_uowMock.Object, audit);
        _sut = new BoxOfficeManager(_uowMock.Object, _booking, overrides, audit, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), gateways, TimeProvider.System);

        _staff = NewUser(RoleNames.BoxOfficeStaff, _theaterId);
        _manager = NewUser(RoleNames.TheaterManager, _theaterId);
        PasswordHasher.CreateHash(_pin, out var hash, out var salt);
        _manager.OverridePinHash = hash;
        _manager.OverridePinSalt = salt;

        _drawer = new CashDrawerSession { Id = Guid.NewGuid(), TheaterId = _theaterId, UserId = _staff.Id, TerminalName = "POS-1", Status = CashDrawerStatus.Open };
        _uowMock.Setup(u => u.CashDrawerStore.GetOpenForUserAsync(_staff.Id)).ReturnsAsync(_drawer);
        _uowMock.Setup(u => u.CashDrawerStore.StageMovement(It.IsAny<CashMovement>())).Callback<CashMovement>(m => _cashMovements.Add(m));
        _uowMock.Setup(u => u.CashDrawerStore.GetTotalsByTypeAsync(It.IsAny<Guid>())).ReturnsAsync(new Dictionary<CashMovementType, double>());
        _uowMock.Setup(u => u.CashDrawerStore.GetRecentMovementsAsync(It.IsAny<Guid>(), It.IsAny<int>())).ReturnsAsync(new List<CashMovement>());
        _uowMock.Setup(u => u.AuditLogStore.Stage(It.IsAny<AuditLog>())).Callback<AuditLog>(a => _audits.Add(a));
        _uowMock.Setup(u => u.InvoiceStore.CreateAsync(It.IsAny<Invoice>()))
            .Callback<Invoice>(i => _created.Add(i))
            .ReturnsAsync((Invoice i) => i);
        _uowMock.Setup(u => u.StockMovementStore.CreateRangeAsync(It.IsAny<List<StockMovement>>()))
            .Callback<List<StockMovement>>(m => _stockMovements.AddRange(m))
            .ReturnsAsync((List<StockMovement> m) => m);
        _uowMock.Setup(u => u.UserStore.ExistsAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(true);

        // Pricing context: one Standard "Adult" category at 100,000 VND in an Active room of the theater.
        var showTime = new ShowTime { Id = _showTimeId, StartTime = DateTime.Now.AddHours(1), EndTime = DateTime.Now.AddHours(3), IsActive = true };
        var room = new Room { Id = _roomId, TheaterId = _theaterId, RoomTypeId = _roomTypeId, Status = RoomStatus.Active };
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(_showTimeId, _roomId))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = _showTimeId, RoomId = _roomId, BasePrice = 0, ShowTime = showTime, Room = room });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(_roomId)).ReturnsAsync(room);
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdAsync(_showTimeId)).ReturnsAsync(showTime);
        _uowMock.Setup(u => u.HolidayStore.FindAsync(It.IsAny<Expression<Func<Holiday, bool>>>())).ReturnsAsync(new List<Holiday>());
        _uowMock.Setup(u => u.TimeSlotStore.FindAsync(It.IsAny<Expression<Func<TimeSlot, bool>>>())).ReturnsAsync(new List<TimeSlot>());
        _uowMock.Setup(u => u.TicketPriceStore.FindAsync(It.IsAny<Expression<Func<TicketPrice, bool>>>())).ReturnsAsync(new List<TicketPrice>());
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(_theaterId))
            .ReturnsAsync(new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = _standardSeatType });
        var adult = new PatronCategory { Id = _adultId, TheaterId = _theaterId, SeatTypeId = _standardSeatType, Name = "Adult", Price = 100000, IsActive = true };
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>())).ReturnsAsync(new List<PatronCategory> { adult });
        _uowMock.Setup(u => u.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<RoomTypePatronCategoryPrice> { new() { RoomTypeId = _roomTypeId, PatronCategoryId = _adultId, Price = 100000 } });
        _uowMock.Setup(u => u.SeatStore.FindAsync(It.IsAny<Expression<Func<Seat, bool>>>())).ReturnsAsync(new List<Seat>());
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(_showTimeId, _roomId)).ReturnsAsync(new List<Guid>());
        SetSeats(new Seat { Id = _seatA, RoomId = _roomId, RowName = "A", ColIndex = 1 }, new Seat { Id = _seatB, RoomId = _roomId, RowName = "A", ColIndex = 2 });
        _uowMock.Setup(u => u.DiscountStore.GetActiveAutoApplyAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<Discount>());
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(It.IsAny<Expression<Func<MemberShip, bool>>>())).ReturnsAsync(new List<MemberShip>());

        _cola = new FoodAndDrink { Id = Guid.NewGuid(), TheaterId = _theaterId, Name = "Cola", Price = 30000, TrackInventory = true, QuantityOnHand = 10, IsAvailable = true };
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids) => new[] { _cola }.Where(f => ids.Contains(f.Id)).ToDictionary(f => f.Id));
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>())).ReturnsAsync(true);
        _uowMock.Setup(u => u.ComboItemStore.GetByCombosAsync(It.IsAny<IReadOnlyCollection<Guid>>())).ReturnsAsync(new List<ComboItem>());
    }

    private User NewUser(string role, Guid? theaterId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = role,
            Status = UserStatus.Active,
            TheaterId = theaterId,
            UserType = new UserType { Id = Guid.NewGuid(), Name = role }
        };
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(user.Id)).ReturnsAsync(user);
        return user;
    }

    private void SetSeats(params Seat[] seats)
    {
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(seats.ToDictionary(s => s.Id));
    }

    private CounterSaleRequest SeatSale(params Guid[] seatIds)
    {
        return new CounterSaleRequest
        {
            ShowTimeId = _showTimeId,
            RoomId = _roomId,
            Seats = seatIds.Select(id => new CounterSeatItem { SeatId = id, PatronCategoryId = _adultId }).ToList(),
        };
    }

    private static TenderLine Cash(double amount) => new() { Method = PaymentTender.Cash, Amount = amount };

    private static TenderLine Card(double amount, string reference = "TERM-1") => new() { Method = PaymentTender.Card, Amount = amount, Reference = reference };

    // ── Sell ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sell_SplitCashAndCard_CreatesPaidCounterInvoiceWithTenders()
    {
        var request = SeatSale(_seatA, _seatB);
        request.Tenders = new List<TenderLine> { Card(100000), Cash(100000) };

        var result = await _sut.SellAsync(_theaterId, _staff.Id, request);

        var invoice = _created.Should().ContainSingle().Subject;
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.Channel.Should().Be(SalesChannel.Counter);
        invoice.TheaterId.Should().Be(_theaterId);
        invoice.SoldByUserId.Should().Be(_staff.Id);
        invoice.CashDrawerSessionId.Should().Be(_drawer.Id);
        invoice.PaidAt.Should().NotBeNull();
        invoice.FinalAmount.Should().Be(200000);
        invoice.InvoiceTickets.Should().HaveCount(2);
        invoice.Payments.Should().HaveCount(2);
        invoice.Payments.Should().ContainSingle(p => p.Method == PaymentTender.Card && p.Amount == 100000 && p.Reference == "TERM-1");
        invoice.Payments.Should().ContainSingle(p => p.Method == PaymentTender.Cash && p.Amount == 100000 && p.TenderedAmount == 100000 && p.ChangeAmount == 0);
        _cashMovements.Should().ContainSingle(m => m.Type == CashMovementType.Sale && m.Amount == 100000 && m.CashDrawerSessionId == _drawer.Id && m.InvoiceId == invoice.Id);
        result.ChangeDue.Should().Be(0);
        result.Tickets.Should().HaveCount(2);

        // A counter sale is Paid straight away and goes through one transaction; seats are announced afterwards.
        _uowMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Never);
        _seatNotifications.Verify(n => n.NotifySeatsBookedAsync(_showTimeId, _roomId, It.Is<IReadOnlyList<Guid>>(l => l.Count == 2)), Times.Once);
    }

    [Fact]
    public async Task Sell_CashShortfall_IsRejectedAndNothingIsSaved()
    {
        var request = SeatSale(_seatA);
        request.Tenders = new List<TenderLine> { Cash(50000) };

        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*short by 50000*");

        _created.Should().BeEmpty();
        _cashMovements.Should().BeEmpty();
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task Sell_CashOverpayment_ReturnsChangeAndRecordsOnlyTheAmountDueInTheDrawer()
    {
        var request = SeatSale(_seatA);
        request.Tenders = new List<TenderLine> { Cash(150000) };

        var result = await _sut.SellAsync(_theaterId, _staff.Id, request);

        result.ChangeDue.Should().Be(50000);
        var cash = _created.Single().Payments.Single();
        cash.Amount.Should().Be(100000);
        cash.TenderedAmount.Should().Be(150000);
        cash.ChangeAmount.Should().Be(50000);
        _cashMovements.Should().ContainSingle().Which.Amount.Should().Be(100000);
    }

    [Fact]
    public async Task Sell_CashWithoutAnOpenDrawer_IsRejected()
    {
        _uowMock.Setup(u => u.CashDrawerStore.GetOpenForUserAsync(_staff.Id)).ReturnsAsync((CashDrawerSession?)null);
        var request = SeatSale(_seatA);
        request.Tenders = new List<TenderLine> { Cash(100000) };

        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*cash drawer*");
        _created.Should().BeEmpty();
    }

    [Fact]
    public async Task Sell_CardOnlyWithoutADrawer_Succeeds()
    {
        _uowMock.Setup(u => u.CashDrawerStore.GetOpenForUserAsync(_staff.Id)).ReturnsAsync((CashDrawerSession?)null);
        var request = SeatSale(_seatA);
        request.Tenders = new List<TenderLine> { Card(100000) };

        await _sut.SellAsync(_theaterId, _staff.Id, request);

        _created.Single().CashDrawerSessionId.Should().BeNull();
        _cashMovements.Should().BeEmpty();
    }

    [Fact]
    public async Task Sell_FoodOnly_DeductsStockAndNeedsNoSeats()
    {
        var request = new CounterSaleRequest
        {
            Foods = new List<CounterFoodItem> { new() { FoodAndDrinkId = _cola.Id, Quantity = 2 } },
            Tenders = new List<TenderLine> { Cash(60000) },
        };

        var result = await _sut.SellAsync(_theaterId, _staff.Id, request);

        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(_cola.Id, -2), Times.Once);
        _stockMovements.Should().ContainSingle(m => m.FoodAndDrinkId == _cola.Id && m.Quantity == -2 && m.UserId == _staff.Id);
        var invoice = _created.Single();
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.InvoiceTickets.Should().BeEmpty();
        invoice.TheaterId.Should().Be(_theaterId);
        result.FinalAmount.Should().Be(60000);
        _seatNotifications.Verify(n => n.NotifySeatsBookedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task Sell_SeatAlreadySold_IsRejected()
    {
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(_showTimeId, _roomId)).ReturnsAsync(new List<Guid> { _seatA });
        var request = SeatSale(_seatA);
        request.Tenders = new List<TenderLine> { Card(100000) };

        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already booked*");
        _created.Should().BeEmpty();
    }

    [Fact]
    public async Task Sell_SeatHeldByAnotherTerminal_IsRejected_ButTheOwnHoldPasses()
    {
        var held = Guid.NewGuid();   // unique id: the lock store is static
        SetSeats(new Seat { Id = held, RoomId = _roomId, RowName = "H", ColIndex = 1 });
        _booking.LockSeat(_showTimeId, _roomId, held, "other-conn");
        var request = SeatSale(held);
        request.ConnectionId = "my-conn";
        request.Tenders = new List<TenderLine> { Card(100000) };

        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*held by another user*");

        request.ConnectionId = "other-conn";
        await _sut.SellAsync(_theaterId, _staff.Id, request);
        _booking.IsSeatLocked(_showTimeId, _roomId, held).Should().BeFalse("the sale releases the terminal's own hold");
    }

    [Fact]
    public async Task Sell_BlockedSeat_IsRejected_OnBothCounterAndOnlinePaths()
    {
        var blocked = Guid.NewGuid();
        SetSeats(new Seat { Id = blocked, RoomId = _roomId, RowName = "Z", ColIndex = 1, IsActive = false });
        var counter = SeatSale(blocked);
        counter.Tenders = new List<TenderLine> { Card(100000) };

        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, counter))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*blocked*");

        var online = new CreateBookingRequest
        {
            ShowTimeId = _showTimeId,
            RoomId = _roomId,
            Seats = new List<BookingSeatItem> { new() { SeatId = blocked, PatronCategoryId = _adultId } },
            PaymentMethod = "Sandbox",
        };
        await FluentActions.Awaiting(() => _booking.CreateBookingAsync(Guid.NewGuid(), online))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*blocked*");
        _created.Should().BeEmpty();
    }

    [Fact]
    public async Task Sell_ShowtimeOfAnotherTheater_IsForbidden()
    {
        var otherTheater = Guid.NewGuid();
        var request = SeatSale(_seatA);
        request.Tenders = new List<TenderLine> { Card(100000) };

        await FluentActions.Awaiting(() => _sut.SellAsync(otherTheater, _staff.Id, request))
            .Should().ThrowAsync<AccessDeniedException>();
        _created.Should().BeEmpty();
    }

    [Fact]
    public async Task Sell_ShowtimeThatHasEnded_IsRejected()
    {
        var ended = new ShowTime { Id = _showTimeId, StartTime = DateTime.Now.AddHours(-3), EndTime = DateTime.Now.AddHours(-1), IsActive = true };
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(_showTimeId, _roomId))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = _showTimeId, RoomId = _roomId, ShowTime = ended, Room = new Room { Id = _roomId, TheaterId = _theaterId, Status = RoomStatus.Active } });
        var request = SeatSale(_seatA);
        request.Tenders = new List<TenderLine> { Card(100000) };

        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*not open for sale*");
    }

    // ── Walk-in vs member ────────────────────────────────────────────────────────

    [Fact]
    public async Task Sell_WalkIn_HasNoUser_NoPointsAndNoEmail()
    {
        var request = SeatSale(_seatA);
        request.Tenders = new List<TenderLine> { Card(100000) };

        await _sut.SellAsync(_theaterId, _staff.Id, request);

        _created.Single().UserId.Should().BeNull();
        _uowMock.Verify(u => u.UserStore.UpdateAsync(It.IsAny<User>()), Times.Never);
        _notifications.Verify(n => n.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _sms.Verify(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Sell_WithAttachedMember_AccruesPointsAndSendsConfirmation()
    {
        var member = NewUser(RoleNames.Customer, null);
        member.Email = "member@cinema.vn";
        member.Points = 3;
        var request = SeatSale(_seatA);
        request.CustomerUserId = member.Id;
        request.Tenders = new List<TenderLine> { Card(100000) };

        await _sut.SellAsync(_theaterId, _staff.Id, request);

        _created.Single().UserId.Should().Be(member.Id);
        member.Points.Should().Be(3 + 10);   // 1 point per 10,000 VND
        _notifications.Verify(n => n.SendAsync("member@cinema.vn", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Sell_RedeemingPointsWithoutAMember_IsRejected()
    {
        var request = SeatSale(_seatA);
        request.PointsToRedeem = 5;
        request.Tenders = new List<TenderLine> { Card(100000) };

        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*member*");
    }

    [Fact]
    public async Task Sell_PointsAndGiftCard_AreRecordedAsPaymentRowsAndReduceTheAmountToTender()
    {
        var member = NewUser(RoleNames.Customer, null);
        member.Points = 10;
        _uowMock.Setup(u => u.GiftCardStore.GetByCodeAsync("GC1"))
            .ReturnsAsync(new GiftCard { Id = Guid.NewGuid(), Code = "GC1", IsActive = true, Balance = 20000 });
        var request = SeatSale(_seatA);
        request.CustomerUserId = member.Id;
        request.PointsToRedeem = 5;
        request.GiftCardCode = "GC1";
        request.Tenders = new List<TenderLine> { Card(75000) };

        var result = await _sut.SellAsync(_theaterId, _staff.Id, request);

        result.FinalAmount.Should().Be(75000);
        var invoice = _created.Single();
        invoice.PointsRedeemed.Should().Be(5);
        invoice.Payments.Should().ContainSingle(p => p.Method == PaymentTender.Points && p.Amount == 5000);
        invoice.Payments.Should().ContainSingle(p => p.Method == PaymentTender.GiftCard && p.Amount == 20000 && p.Reference == "GC1");
        invoice.Payments.Should().ContainSingle(p => p.Method == PaymentTender.Card && p.Amount == 75000);
    }

    // ── Price override ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Sell_PriceOverrideWithoutApproverPin_IsForbidden_AndNothingIsSaved()
    {
        var request = SeatSale(_seatA);
        request.Seats[0].OverrideUnitPrice = 80000;
        request.Tenders = new List<TenderLine> { Card(80000) };

        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, request))
            .Should().ThrowAsync<AccessDeniedException>();

        _created.Should().BeEmpty();
        _uowMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task Sell_PriceOverrideWithWrongPin_IsForbidden()
    {
        var request = SeatSale(_seatA);
        request.Seats[0].OverrideUnitPrice = 80000;
        request.Override = new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = "0000" };
        request.Tenders = new List<TenderLine> { Card(80000) };

        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, request))
            .Should().ThrowAsync<AccessDeniedException>();
        _created.Should().BeEmpty();
    }

    [Fact]
    public async Task Sell_PriceOverrideWithApproverPin_AppliesTheLowerPriceAndIsAudited()
    {
        var request = SeatSale(_seatA);
        request.Seats[0].OverrideUnitPrice = 80000;
        request.Override = new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin };
        request.Tenders = new List<TenderLine> { Card(80000) };

        var result = await _sut.SellAsync(_theaterId, _staff.Id, request);

        result.FinalAmount.Should().Be(80000);
        _created.Single().InvoiceTickets.Single().Price.Should().Be(80000);
        var audit = _audits.Should().ContainSingle(a => a.Action == AuditAction.PriceOverride).Subject;
        audit.ActorUserId.Should().Be(_staff.Id);
        audit.ApproverUserId.Should().Be(_manager.Id);
        audit.TheaterId.Should().Be(_theaterId);
        audit.EntityId.Should().Be(_created.Single().Id);
        audit.Amount.Should().Be(20000);
    }

    [Fact]
    public async Task Sell_PriceOverrideAboveTheListPrice_IsRejected()
    {
        var request = SeatSale(_seatA);
        request.Seats[0].OverrideUnitPrice = 120000;
        request.Override = new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin };
        request.Tenders = new List<TenderLine> { Card(120000) };

        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*list price*");
    }

    [Fact]
    public async Task Sell_ByAManager_NeedsNoPinForAnOverride_ButIsStillAudited()
    {
        var request = SeatSale(_seatA);
        request.Seats[0].OverrideUnitPrice = 90000;
        request.Tenders = new List<TenderLine> { Card(90000) };

        await _sut.SellAsync(_theaterId, _manager.Id, request);

        _audits.Should().ContainSingle(a => a.Action == AuditAction.PriceOverride && a.ApproverUserId == _manager.Id && a.ActorUserId == _manager.Id);
    }

    // ── Quote ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quote_PricesLikeASale_ButHasNoSideEffects()
    {
        var member = NewUser(RoleNames.Customer, null);
        member.Points = 10;
        var card = new GiftCard { Id = Guid.NewGuid(), Code = "GC1", IsActive = true, Balance = 20000 };
        _uowMock.Setup(u => u.GiftCardStore.GetByCodeAsync("GC1")).ReturnsAsync(card);
        var request = SeatSale(_seatA);
        request.Foods = new List<CounterFoodItem> { new() { FoodAndDrinkId = _cola.Id, Quantity = 2 } };
        request.CustomerUserId = member.Id;
        request.PointsToRedeem = 5;
        request.GiftCardCode = "GC1";

        var quote = await _sut.QuoteAsync(_theaterId, request);

        quote.TotalAmount.Should().Be(160000);
        quote.PointsValue.Should().Be(5000);
        quote.GiftCardAmount.Should().Be(20000);
        quote.DiscountAmount.Should().Be(0);
        quote.FinalAmount.Should().Be(135000);
        quote.Lines.Should().HaveCount(2);

        member.Points.Should().Be(10);
        card.Balance.Should().Be(20000);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        _uowMock.Verify(u => u.UserStore.UpdateAsync(It.IsAny<User>()), Times.Never);
        _uowMock.Verify(u => u.GiftCardStore.UpdateAsync(It.IsAny<GiftCard>()), Times.Never);
        _uowMock.Verify(u => u.InvoiceStore.CreateAsync(It.IsAny<Invoice>()), Times.Never);
        _uowMock.Verify(u => u.StockMovementStore.CreateRangeAsync(It.IsAny<List<StockMovement>>()), Times.Never);
        _uowMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task Quote_StillRejectsUnavailableSeats()
    {
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(_showTimeId, _roomId)).ReturnsAsync(new List<Guid> { _seatA });

        await FluentActions.Awaiting(() => _sut.QuoteAsync(_theaterId, SeatSale(_seatA)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already booked*");
    }

    // ── Online booking is unchanged ──────────────────────────────────────────────

    [Fact]
    public async Task OnlineBooking_StillCreatesAPendingInvoiceWithTheaterAndChannel()
    {
        var customer = Guid.NewGuid();
        var request = new CreateBookingRequest
        {
            ShowTimeId = _showTimeId,
            RoomId = _roomId,
            Seats = new List<BookingSeatItem> { new() { SeatId = _seatA, PatronCategoryId = _adultId } },
            PaymentMethod = "Sandbox",
        };

        var result = await _booking.CreateBookingAsync(customer, request);

        result.Status.Should().Be(InvoiceStatus.Pending);
        var invoice = _created.Single();
        invoice.Status.Should().Be(InvoiceStatus.Pending);
        invoice.Channel.Should().Be(SalesChannel.Online);
        invoice.TheaterId.Should().Be(_theaterId);
        invoice.UserId.Should().Be(customer);
        invoice.PaymentMethod.Should().Be("Sandbox");
        invoice.Payments.Should().BeEmpty();
        invoice.PaidAt.Should().BeNull();
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
        _cashMovements.Should().BeEmpty();
    }

    [Fact]
    public async Task OnlineRefund_RefusesACounterInvoice()
    {
        var invoice = new Invoice { Id = Guid.NewGuid(), Status = InvoiceStatus.Paid, Channel = SalesChannel.Counter, UserId = null };
        _uowMock.Setup(u => u.InvoiceStore.GetWithDetailsAsync(invoice.Id)).ReturnsAsync(invoice);

        var refunded = await _booking.RefundBookingAsync(Guid.NewGuid(), invoice.Id, isAdmin: true);

        refunded.Should().BeFalse();
        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    // ── Tender rules ─────────────────────────────────────────────────────────────

    [Fact]
    public void Settle_CardNeedsAReference()
    {
        FluentActions.Invoking(() => TenderSettlement.Settle(100000, new List<TenderLine> { new() { Method = PaymentTender.Card, Amount = 100000 } }))
            .Should().Throw<InvalidOperationException>().WithMessage("*reference*");
    }

    [Fact]
    public void Settle_CardAndQrCannotExceedTheAmountDue()
    {
        FluentActions.Invoking(() => TenderSettlement.Settle(100000, new List<TenderLine> { Card(60000), new() { Method = PaymentTender.QrWallet, Amount = 60000, Reference = "Q1" } }))
            .Should().Throw<InvalidOperationException>().WithMessage("*exceed*");
    }

    [Theory]
    [InlineData(PaymentTender.Points)]
    [InlineData(PaymentTender.GiftCard)]
    [InlineData(PaymentTender.Online)]
    public void Settle_OnlyCashCardAndQrCanBeTendered(PaymentTender method)
    {
        FluentActions.Invoking(() => TenderSettlement.Settle(100000, new List<TenderLine> { new() { Method = method, Amount = 100000, Reference = "X" } }))
            .Should().Throw<InvalidOperationException>().WithMessage("*Only Cash, Card and QrWallet*");
    }

    [Fact]
    public void Settle_SplitCardAndCash_ComputesCashDueAndChange()
    {
        var result = TenderSettlement.Settle(100000, new List<TenderLine> { Card(40000), Cash(100000) });

        result.CashApplied.Should().Be(60000);
        result.ChangeDue.Should().Be(40000);
    }

    [Fact]
    public void Settle_NothingDue_NeedsNoTender()
    {
        var result = TenderSettlement.Settle(0, new List<TenderLine>());

        result.Payments.Should().BeEmpty();
        result.CashApplied.Should().Be(0);
    }

    // ── Cash drawer ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenDrawer_StagesTheSessionAndItsOpeningFloat()
    {
        var sessions = new List<CashDrawerSession>();
        _uowMock.Setup(u => u.CashDrawerStore.GetOpenForUserAsync(_staff.Id)).ReturnsAsync((CashDrawerSession?)null);
        _uowMock.Setup(u => u.CashDrawerStore.StageSession(It.IsAny<CashDrawerSession>())).Callback<CashDrawerSession>(s => sessions.Add(s));

        var dto = await _sut.OpenDrawerAsync(_theaterId, _staff.Id, new OpenDrawerRequest { TerminalName = " POS-2 ", OpeningFloat = 500000 });

        var session = sessions.Should().ContainSingle().Subject;
        session.TerminalName.Should().Be("POS-2");
        session.UserId.Should().Be(_staff.Id);
        session.Status.Should().Be(CashDrawerStatus.Open);
        _cashMovements.Should().ContainSingle(m => m.Type == CashMovementType.OpeningFloat && m.Amount == 500000 && m.CashDrawerSessionId == session.Id);
        dto.IsOpen.Should().BeTrue();
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task OpenDrawer_WhenTheUserAlreadyHasOne_IsRejected()
    {
        await FluentActions.Awaiting(() => _sut.OpenDrawerAsync(_theaterId, _staff.Id, new OpenDrawerRequest { TerminalName = "POS-9", OpeningFloat = 0 }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already have*");
    }

    [Fact]
    public async Task OpenDrawer_WhenTheTerminalIsInUse_IsRejected()
    {
        _uowMock.Setup(u => u.CashDrawerStore.GetOpenForUserAsync(_staff.Id)).ReturnsAsync((CashDrawerSession?)null);
        _uowMock.Setup(u => u.CashDrawerStore.IsTerminalInUseAsync(_theaterId, "POS-1")).ReturnsAsync(true);

        await FluentActions.Awaiting(() => _sut.OpenDrawerAsync(_theaterId, _staff.Id, new OpenDrawerRequest { TerminalName = "POS-1", OpeningFloat = 0 }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*terminal*");
    }

    [Fact]
    public async Task GetMyDrawer_WithoutAnOpenDrawer_ReportsClosed()
    {
        _uowMock.Setup(u => u.CashDrawerStore.GetOpenForUserAsync(_staff.Id)).ReturnsAsync((CashDrawerSession?)null);

        (await _sut.GetMyDrawerAsync(_theaterId, _staff.Id)).IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task PayOut_WithoutManagerApproval_IsForbidden()
    {
        await FluentActions.Awaiting(() => _sut.PayInOutAsync(_theaterId, _staff.Id,
                new PayInOutRequest { Type = CashMovementType.PayOut, Amount = 10000, Note = "Supplies" }))
            .Should().ThrowAsync<AccessDeniedException>();
        _cashMovements.Should().BeEmpty();
    }

    [Fact]
    public async Task PayOut_WithApproverPin_RecordsANegativeMovementAndAuditsIt()
    {
        _uowMock.Setup(u => u.CashDrawerStore.GetTotalsByTypeAsync(_drawer.Id))
            .ReturnsAsync(new Dictionary<CashMovementType, double> { [CashMovementType.OpeningFloat] = 500000 });

        await _sut.PayInOutAsync(_theaterId, _staff.Id, new PayInOutRequest
        {
            Type = CashMovementType.PayOut,
            Amount = 10000,
            Note = "Supplies",
            Override = new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin },
        });

        _cashMovements.Should().ContainSingle(m => m.Type == CashMovementType.PayOut && m.Amount == -10000);
        _audits.Should().ContainSingle(a => a.Action == AuditAction.CashPayOut && a.ApproverUserId == _manager.Id && a.Amount == 10000);
    }

    [Fact]
    public async Task PayOut_MoreThanTheDrawerHolds_IsRejected()
    {
        await FluentActions.Awaiting(() => _sut.PayInOutAsync(_theaterId, _manager.Id,
                new PayInOutRequest { Type = CashMovementType.PayOut, Amount = 10000, Note = "Supplies" }))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PayIn_NeedsNoApproval()
    {
        await _sut.PayInOutAsync(_theaterId, _staff.Id, new PayInOutRequest { Type = CashMovementType.PayIn, Amount = 50000, Note = "Float top-up" });

        _cashMovements.Should().ContainSingle(m => m.Type == CashMovementType.PayIn && m.Amount == 50000);
    }

    // ── Request shape ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sell_EmptyCart_IsRejected()
    {
        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, new CounterSaleRequest()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Sell_SeatsWithoutAShowtime_AreRejected()
    {
        var request = new CounterSaleRequest { Seats = new List<CounterSeatItem> { new() { SeatId = _seatA, PatronCategoryId = _adultId } } };

        await FluentActions.Awaiting(() => _sut.SellAsync(_theaterId, _staff.Id, request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*showtime*");
    }

    [Fact]
    public async Task GetShowtimesToday_MarksEndedShowtimes()
    {
        var rows = new List<TheaterShowTimeRow>
        {
            new(Guid.NewGuid(), _roomId, "Room 1", Guid.NewGuid(), "Past", DateTime.Now.AddHours(-3), DateTime.Now.AddHours(-1), ProjectionForm.TwoD),
            new(Guid.NewGuid(), _roomId, "Room 1", Guid.NewGuid(), "Next", DateTime.Now.AddHours(1), DateTime.Now.AddHours(3), ProjectionForm.TwoD),
        };
        _uowMock.Setup(u => u.ShowTimeStore.GetByTheaterAndRangeAsync(_theaterId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(rows);

        var result = await _sut.GetShowtimesTodayAsync(_theaterId, null);

        result.Select(r => r.HasEnded).Should().Equal(true, false);
    }

    // ── After-sales (P5) ─────────────────────────────────────────────────────────

    private Invoice PaidCounterInvoice(Guid seatId, DateTime showStartLocal, Guid? userId = null, double final = 100000, bool withFood = false)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Code = "INV-" + Guid.NewGuid().ToString("N")[..6],
            UserId = userId,
            TheaterId = _theaterId,
            Status = InvoiceStatus.Paid,
            Channel = SalesChannel.Counter,
            FinalAmount = final,
            TotalAmount = final,
            PaidAt = DateTime.UtcNow,
        };
        var showTime = new ShowTime { Id = _showTimeId, StartTime = showStartLocal, EndTime = showStartLocal.AddHours(2) };
        invoice.InvoiceTickets.Add(new InvoiceTicket
        {
            InvoiceId = invoice.Id, ShowTimeId = _showTimeId, RoomId = _roomId, SeatId = seatId, Price = final, IsActive = true,
            ShowTimeRoom = new ShowTimeRoom { ShowTimeId = _showTimeId, RoomId = _roomId, ShowTime = showTime },
            Seat = new Seat { Id = seatId, RowName = "A", ColIndex = 1 },
        });
        _uowMock.Setup(u => u.InvoiceStore.GetWithDetailsAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.AfterSalesStore.TryClaimRefundAsync(invoice.Id, It.IsAny<StaffReasonCode?>(), It.IsAny<DateTime>())).ReturnsAsync(true);
        if (withFood)
        {
            _uowMock.Setup(u => u.StockMovementStore.GetNetSaleQuantitiesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
                .ReturnsAsync(new List<StockNetQuantity> { new(invoice.Id, _cola.Id, -2) });
        }
        return invoice;
    }

    private static DateTime LocalNow() => Cinema.Business.Helpers.BusinessCalendar.ToLocal(DateTime.UtcNow);

    private StaffRefundRequest RefundRequest(Invoice invoice, ManagerOverrideDTO? approval = null)
    {
        return new StaffRefundRequest { InvoiceId = invoice.Id, ReasonCode = StaffReasonCode.CustomerRequest, RefundTender = PaymentTender.Cash, Override = approval };
    }

    [Fact]
    public async Task StaffRefund_ByBoxOfficeStaffWithoutPin_IsForbidden()
    {
        var invoice = PaidCounterInvoice(_seatA, LocalNow().AddHours(2));

        await FluentActions.Awaiting(() => _sut.StaffRefundAsync(_theaterId, _staff.Id, RefundRequest(invoice)))
            .Should().ThrowAsync<AccessDeniedException>();

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        _uowMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task StaffRefund_WithManagerPin_RefundsFreesSeatsRestoresStockAndPaysCashFromTheDrawer()
    {
        var invoice = PaidCounterInvoice(_seatA, LocalNow().AddHours(2), withFood: true);
        _uowMock.Setup(u => u.CashDrawerStore.GetTotalsByTypeAsync(_drawer.Id))
            .ReturnsAsync(new Dictionary<CashMovementType, double> { [CashMovementType.Sale] = 300000 });
        var approval = new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin };

        var result = await _sut.StaffRefundAsync(_theaterId, _staff.Id, RefundRequest(invoice, approval));

        result.RefundedAmount.Should().Be(100000);
        result.CashReturned.Should().Be(100000);
        invoice.Status.Should().Be(InvoiceStatus.Refunded);
        invoice.RefundReasonCode.Should().Be(StaffReasonCode.CustomerRequest);
        _uowMock.Verify(u => u.InvoiceStore.DeactivateTicketsAsync(invoice.Id), Times.Once);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(_cola.Id, 2), Times.Once);
        _stockMovements.Should().ContainSingle(m => m.Type == StockMovementType.SaleReversal && m.Quantity == 2 && m.InvoiceId == invoice.Id);
        _cashMovements.Should().ContainSingle(m => m.Type == CashMovementType.Refund && m.Amount == -100000 && m.InvoiceId == invoice.Id);
        _audits.Should().ContainSingle(a => a.Action == AuditAction.Refund && a.ActorUserId == _staff.Id && a.ApproverUserId == _manager.Id && a.EntityId == invoice.Id);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task StaffRefund_ByManagerNeedsNoPin()
    {
        var invoice = PaidCounterInvoice(_seatA, LocalNow().AddHours(2));
        _uowMock.Setup(u => u.CashDrawerStore.GetOpenForUserAsync(_manager.Id))
            .ReturnsAsync(new CashDrawerSession { Id = Guid.NewGuid(), TheaterId = _theaterId, UserId = _manager.Id, Status = CashDrawerStatus.Open });
        _uowMock.Setup(u => u.CashDrawerStore.GetTotalsByTypeAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new Dictionary<CashMovementType, double> { [CashMovementType.Sale] = 300000 });

        await _sut.StaffRefundAsync(_theaterId, _manager.Id, RefundRequest(invoice));

        _audits.Should().ContainSingle(a => a.ActorUserId == _manager.Id && a.ApproverUserId == _manager.Id);
    }

    [Fact]
    public async Task StaffRefund_WalkIn_SkipsUserSteps()
    {
        var invoice = PaidCounterInvoice(_seatA, LocalNow().AddHours(2));
        invoice.UserId.Should().BeNull();
        _uowMock.Setup(u => u.CashDrawerStore.GetTotalsByTypeAsync(_drawer.Id))
            .ReturnsAsync(new Dictionary<CashMovementType, double> { [CashMovementType.Sale] = 300000 });

        await _sut.StaffRefundAsync(_theaterId, _staff.Id, RefundRequest(invoice, new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin }));

        _uowMock.Verify(u => u.UserStore.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task StaffRefund_AfterShowStart_NeedsAnOverride_AndIsFlaggedInTheResult()
    {
        var invoice = PaidCounterInvoice(_seatA, LocalNow().AddMinutes(-10));
        _uowMock.Setup(u => u.CashDrawerStore.GetTotalsByTypeAsync(_drawer.Id))
            .ReturnsAsync(new Dictionary<CashMovementType, double> { [CashMovementType.Sale] = 300000 });

        await FluentActions.Awaiting(() => _sut.StaffRefundAsync(_theaterId, _staff.Id, RefundRequest(invoice)))
            .Should().ThrowAsync<AccessDeniedException>();

        var result = await _sut.StaffRefundAsync(_theaterId, _staff.Id, RefundRequest(invoice, new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin }));
        result.AfterShowStart.Should().BeTrue();
    }

    [Fact]
    public async Task StaffRefund_AlreadyClaimedByAnotherRequest_RollsBackWithoutPayingOut()
    {
        var invoice = PaidCounterInvoice(_seatA, LocalNow().AddHours(2));
        _uowMock.Setup(u => u.AfterSalesStore.TryClaimRefundAsync(invoice.Id, It.IsAny<StaffReasonCode?>(), It.IsAny<DateTime>())).ReturnsAsync(false);
        _uowMock.Setup(u => u.CashDrawerStore.GetTotalsByTypeAsync(_drawer.Id))
            .ReturnsAsync(new Dictionary<CashMovementType, double> { [CashMovementType.Sale] = 300000 });

        await FluentActions.Awaiting(() => _sut.StaffRefundAsync(_theaterId, _staff.Id, RefundRequest(invoice, new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin })))
            .Should().ThrowAsync<InvalidOperationException>();

        _cashMovements.Should().BeEmpty();
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task Exchange_ReversesTheOldInvoiceAndSellsTheNewOneInOneTransaction()
    {
        var old = PaidCounterInvoice(_seatA, LocalNow().AddHours(2));
        var request = new ExchangeRequest
        {
            InvoiceId = old.Id,
            ReasonCode = StaffReasonCode.WrongShowtime,
            NewSale = SeatSale(_seatB),
            Override = new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin },
        };

        var result = await _sut.ExchangeAsync(_theaterId, _staff.Id, request);

        old.Status.Should().Be(InvoiceStatus.Refunded);
        var created = _created.Should().ContainSingle().Subject;
        created.ExchangedFromInvoiceId.Should().Be(old.Id);
        created.Status.Should().Be(InvoiceStatus.Paid);
        result.AmountCollected.Should().Be(0);
        result.RefundedBack.Should().Be(0);
        _uowMock.Verify(u => u.InvoiceStore.DeactivateTicketsAsync(old.Id), Times.Once);
        _uowMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
        _audits.Should().ContainSingle(a => a.Action == AuditAction.Exchange && a.ApproverUserId == _manager.Id && a.ActorUserId == _staff.Id);
    }

    [Fact]
    public async Task Exchange_ToACheaperSale_PaysTheDifferenceBackFromTheDrawer()
    {
        var old = PaidCounterInvoice(_seatA, LocalNow().AddHours(2), final: 160000);
        _uowMock.Setup(u => u.CashDrawerStore.GetTotalsByTypeAsync(_drawer.Id))
            .ReturnsAsync(new Dictionary<CashMovementType, double> { [CashMovementType.Sale] = 300000 });
        var request = new ExchangeRequest
        {
            InvoiceId = old.Id,
            ReasonCode = StaffReasonCode.WrongShowtime,
            NewSale = SeatSale(_seatB),
            Override = new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin },
        };

        var result = await _sut.ExchangeAsync(_theaterId, _staff.Id, request);

        result.RefundedBack.Should().Be(60000);
        _cashMovements.Should().ContainSingle(m => m.Type == CashMovementType.Refund && m.Amount == -60000);
    }

    [Fact]
    public async Task Exchange_WhenTheNewSeatIsTaken_RollsBackAndKeepsTheOldInvoice()
    {
        var old = PaidCounterInvoice(_seatA, LocalNow().AddHours(2));
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(_showTimeId, _roomId)).ReturnsAsync(new List<Guid> { _seatB });
        var request = new ExchangeRequest
        {
            InvoiceId = old.Id,
            ReasonCode = StaffReasonCode.WrongShowtime,
            NewSale = SeatSale(_seatB),
            Override = new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin },
        };

        await FluentActions.Awaiting(() => _sut.ExchangeAsync(_theaterId, _staff.Id, request))
            .Should().ThrowAsync<InvalidOperationException>();

        _created.Should().BeEmpty();
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task Reprint_UsedTicketNeedsAnOverride_UnusedDoesNot_AndBothAreAudited()
    {
        var invoice = PaidCounterInvoice(_seatA, LocalNow().AddHours(2));
        invoice.InvoiceTickets.Single().QrCode = "QR1";

        var ok = await _sut.ReprintAsync(_theaterId, _staff.Id, new ReprintRequest { InvoiceId = invoice.Id, Reason = "Printer jam" });
        ok.Tickets.Should().ContainSingle(t => t.QrCode == "QR1" && t.SeatLabel == "A1");
        _audits.Should().ContainSingle(a => a.Action == AuditAction.Reprint && a.ApproverUserId == null);

        invoice.InvoiceTickets.Single().IsUsed = true;
        await FluentActions.Awaiting(() => _sut.ReprintAsync(_theaterId, _staff.Id, new ReprintRequest { InvoiceId = invoice.Id, Reason = "Again" }))
            .Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task CloseDrawer_TenThousandShort_ShowsVarianceAndNeedsReconciliation()
    {
        _uowMock.Setup(u => u.CashDrawerStore.GetByIdAsync(_drawer.Id)).ReturnsAsync(_drawer);
        _uowMock.Setup(u => u.CashDrawerStore.GetTotalsByTypeAsync(_drawer.Id))
            .ReturnsAsync(new Dictionary<CashMovementType, double> { [CashMovementType.OpeningFloat] = 500000, [CashMovementType.Sale] = 200000 });

        var result = await _sut.CloseDrawerAsync(_theaterId, _staff.Id, new CloseDrawerRequest { SessionId = _drawer.Id, CountedCash = 690000 });

        result.ExpectedCash.Should().Be(700000);
        result.Variance.Should().Be(-10000);
        result.NeedsReconciliation.Should().BeTrue();
        _drawer.Status.Should().Be(CashDrawerStatus.Closed);

        var reconcile = new ReconcileDrawerRequest { SessionId = _drawer.Id, Note = "Short count accepted" };
        await FluentActions.Awaiting(() => _sut.ReconcileDrawerAsync(_theaterId, _staff.Id, reconcile))
            .Should().ThrowAsync<AccessDeniedException>();

        reconcile.Override = new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin };
        var reconciled = await _sut.ReconcileDrawerAsync(_theaterId, _staff.Id, reconcile);
        reconciled.Status.Should().Be(CashDrawerStatus.Reconciled);
        _audits.Should().ContainSingle(a => a.Action == AuditAction.DrawerReconcile && a.ApproverUserId == _manager.Id);
    }

    [Fact]
    public async Task CloseDrawer_ExactCount_IsReconciledStraightAway()
    {
        _uowMock.Setup(u => u.CashDrawerStore.GetByIdAsync(_drawer.Id)).ReturnsAsync(_drawer);
        _uowMock.Setup(u => u.CashDrawerStore.GetTotalsByTypeAsync(_drawer.Id))
            .ReturnsAsync(new Dictionary<CashMovementType, double> { [CashMovementType.OpeningFloat] = 500000 });

        var result = await _sut.CloseDrawerAsync(_theaterId, _staff.Id, new CloseDrawerRequest { SessionId = _drawer.Id, CountedCash = 500000 });

        result.NeedsReconciliation.Should().BeFalse();
        result.Status.Should().Be(CashDrawerStatus.Reconciled);
    }

    [Fact]
    public async Task DailyClose_TotalsEqualTheInvoicePaymentSumsOfTheBusinessDay()
    {
        var expectedWindow = Cinema.Business.Helpers.BusinessCalendar.WindowOf(new DateTime(2026, 10, 4), 6);
        expectedWindow.FromUtc.Should().Be(new DateTime(2026, 10, 3, 23, 0, 0), "06:00 in UTC+7 is 23:00 UTC the day before");
        expectedWindow.ToUtc.Should().Be(new DateTime(2026, 10, 4, 23, 0, 0));

        _uowMock.Setup(u => u.AfterSalesStore.GetTenderTotalsAsync(_theaterId, expectedWindow.FromUtc, expectedWindow.ToUtc))
            .ReturnsAsync(new List<TenderTotalRow>
            {
                new() { Method = PaymentTender.Cash, Amount = 300000, Count = 3 },
                new() { Method = PaymentTender.Card, Amount = 200000, Count = 2 },
                new() { Method = PaymentTender.Points, Amount = 5000, Count = 1 },
            });
        _uowMock.Setup(u => u.AfterSalesStore.GetRefundedInvoicesAsync(_theaterId, expectedWindow.FromUtc, expectedWindow.ToUtc))
            .ReturnsAsync(new List<RefundedInvoiceRow>
            {
                new() { Id = Guid.NewGuid(), FinalAmount = 100000 },
                new() { Id = Guid.NewGuid(), FinalAmount = 160000, ReplacementFinalAmount = 100000 },
            });
        _uowMock.Setup(u => u.AfterSalesStore.GetSalesVolumeAsync(_theaterId, expectedWindow.FromUtc, expectedWindow.ToUtc))
            .ReturnsAsync(new SalesVolumeRow { TicketsSold = 5, FoodItemsSold = 4, FoodRevenue = 120000 });
        _uowMock.Setup(u => u.AfterSalesStore.GetAuditTotalsAsync(_theaterId, AuditAction.Compensation, expectedWindow.FromUtc, expectedWindow.ToUtc))
            .ReturnsAsync((1, 50000.0));
        _uowMock.Setup(u => u.AfterSalesStore.GetDrawerSessionsAsync(_theaterId, expectedWindow.FromUtc, expectedWindow.ToUtc))
            .ReturnsAsync(new List<DrawerSessionRow>
            {
                new() { Id = Guid.NewGuid(), TerminalName = "POS-1", Status = CashDrawerStatus.Closed, OpenedAt = DateTime.UtcNow, ExpectedCash = 700000, CountedCash = 690000, Variance = -10000 },
            });
        var daily = new DailyCloseManager(_uowMock.Object, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), TimeProvider.System);

        var close = await daily.GetDailyCloseAsync(_theaterId, new DateTime(2026, 10, 4));

        close.PaymentsTotal.Should().Be(505000);
        close.MoneyCollected.Should().Be(500000);
        close.RefundCount.Should().Be(1);
        close.ExchangeCount.Should().Be(1);
        close.RefundAmount.Should().Be(100000 + 60000);
        close.NetCollected.Should().Be(340000);
        close.TicketsSold.Should().Be(5);
        close.FoodItemsSold.Should().Be(4);
        close.CompAmount.Should().Be(50000);
        close.UnreconciledDrawerCount.Should().Be(1);
        close.TotalVariance.Should().Be(-10000);
    }
}
