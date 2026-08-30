using Cinema.Business.Contracts.Payments;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Managers;
using Cinema.Business.Notifications;
using Cinema.Business.Payments;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class BookingServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly BookingManager _sut;

    private static readonly Guid ShowTimeId1 = Guid.NewGuid();
    private static readonly Guid ShowTimeId2 = Guid.NewGuid();
    private static readonly Guid RoomId1     = Guid.NewGuid();
    private static readonly Guid RoomId2     = Guid.NewGuid();
    private static readonly Guid SeatId1     = Guid.NewGuid();
    private static readonly Guid SeatId5     = Guid.NewGuid();
    private static readonly Guid SeatId10    = Guid.NewGuid();
    private static readonly Guid SeatId11    = Guid.NewGuid();
    private static readonly Guid SeatId20    = Guid.NewGuid();
    private static readonly Guid SeatId21    = Guid.NewGuid();
    private static readonly Guid SeatTypeId1 = Guid.NewGuid();

    public BookingServiceTests()
    {
        var gateways = new PaymentGatewayResolver(new IPaymentGateway[] { new SandboxPaymentGateway() }, "Sandbox");
        _sut = new BookingManager(_uowMock.Object, gateways, new DevLogNotificationService(), new DevLogSmsNotificationService());
    }

    private static PagingSearchDTO SeatSearch(Guid showTimeId, Guid roomId)
    {
        return new()
        {
            Filters = new Dictionary<string, string>
            {
                ["showTimeId"] = showTimeId.ToString(),
                ["roomId"]     = roomId.ToString()
            }
        };
    }

    [Fact]
    public async Task GetSeatsAsync_ReturnsAvailableStatus_WhenNotBookedOrLocked()
    {
        var seats = new List<Seat> { new() { Id = SeatId1, RowName = "A", ColIndex = 1, SeatTypeId = SeatTypeId1 } };
        _uowMock.Setup(u => u.SeatStore.GetByRoomAsync(RoomId1)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1))
            .ReturnsAsync(new ShowTimeRoom { BasePrice = 100 });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Room?)null);

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId1));

        result.Results.Should().HaveCount(1);
        result.Results.First().Status.Should().Be(SeatStatus.Available);
    }

    [Fact]
    public async Task GetSeatsAsync_ReturnsOccupied_WhenSeatIsBooked()
    {
        var seats = new List<Seat> { new() { Id = SeatId5, RowName = "B", ColIndex = 2, SeatTypeId = SeatTypeId1 } };
        _uowMock.Setup(u => u.SeatStore.GetByRoomAsync(RoomId2)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId2)).ReturnsAsync(new List<Guid> { SeatId5 });
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId2))
            .ReturnsAsync(new ShowTimeRoom { BasePrice = 120 });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Room?)null);

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId2));

        result.Results.First().Status.Should().Be(SeatStatus.Occupied);
    }

    [Fact]
    public async Task GetSeatsAsync_UsesTicketPriceMatrix_WhenRowMatches()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var timeSlotId = Guid.NewGuid();
        var seatType   = new SeatType { Id = SeatTypeId1, PriceMultiplier = 2 };
        var seats      = new List<Seat> { new() { Id = SeatId1, RowName = "A", ColIndex = 1, SeatTypeId = SeatTypeId1, SeatType = seatType } };

        _uowMock.Setup(u => u.SeatStore.GetByRoomAsync(RoomId1)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = ShowTimeId1, RoomId = RoomId1, BasePrice = 100 });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(RoomId1))
            .ReturnsAsync(new Room { Id = RoomId1, TheaterId = theaterId, RoomTypeId = roomTypeId });
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdAsync(ShowTimeId1))
            .ReturnsAsync(new ShowTime { Id = ShowTimeId1, StartTime = new DateTime(2026, 3, 2, 19, 0, 0) });
        _uowMock.Setup(u => u.HolidayStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Holiday, bool>>>()))
            .ReturnsAsync(new List<Holiday>());
        _uowMock.Setup(u => u.TimeSlotStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TimeSlot, bool>>>()))
            .ReturnsAsync(new List<TimeSlot> { new() { Id = timeSlotId, TheaterId = theaterId, StartTime = "18:00", EndTime = "22:00" } });
        _uowMock.Setup(u => u.TicketPriceStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TicketPrice, bool>>>()))
            .ReturnsAsync(new List<TicketPrice> { new() { SeatTypeId = SeatTypeId1, TimeSlotId = timeSlotId, IsHoliday = false, Price = 250 } });

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId1));

        // Matrix row (250) wins over BasePrice(100) × multiplier(2) = 200.
        result.Results.First().Price.Should().Be(250);
    }

    [Fact]
    public async Task GetSeatsAsync_AppliesHolidayMultiplier_WhenNoMatrixRow()
    {
        var theaterId = Guid.NewGuid();
        var seatType  = new SeatType { Id = SeatTypeId1, PriceMultiplier = 2 };
        var seats     = new List<Seat> { new() { Id = SeatId1, RowName = "A", ColIndex = 1, SeatTypeId = SeatTypeId1, SeatType = seatType } };

        _uowMock.Setup(u => u.SeatStore.GetByRoomAsync(RoomId1)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = ShowTimeId1, RoomId = RoomId1, BasePrice = 100 });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(RoomId1))
            .ReturnsAsync(new Room { Id = RoomId1, TheaterId = theaterId, RoomTypeId = Guid.NewGuid() });
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdAsync(ShowTimeId1))
            .ReturnsAsync(new ShowTime { Id = ShowTimeId1, StartTime = new DateTime(2026, 1, 1, 19, 0, 0) });
        _uowMock.Setup(u => u.HolidayStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Holiday, bool>>>()))
            .ReturnsAsync(new List<Holiday> { new() { Date = new DateOnly(2026, 1, 1), PriceMultiplier = 1.5 } });
        _uowMock.Setup(u => u.TimeSlotStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TimeSlot, bool>>>()))
            .ReturnsAsync(new List<TimeSlot>());   // no slot → no matrix lookup, fall back

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId1));

        // Fallback: BasePrice(100) × multiplier(2) × holiday(1.5) = 300.
        result.Results.First().Price.Should().Be(300);
    }

    [Fact]
    public async Task GetSeatsAsync_AddsThreeDSurcharge_OnTopOfMatrixPrice()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var timeSlotId = Guid.NewGuid();
        var seatType   = new SeatType { Id = SeatTypeId1, PriceMultiplier = 2 };
        var seats      = new List<Seat> { new() { Id = SeatId1, RowName = "A", ColIndex = 1, SeatTypeId = SeatTypeId1, SeatType = seatType } };

        _uowMock.Setup(u => u.SeatStore.GetByRoomAsync(RoomId1)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = ShowTimeId1, RoomId = RoomId1, BasePrice = 100 });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(RoomId1))
            .ReturnsAsync(new Room { Id = RoomId1, TheaterId = theaterId, RoomTypeId = roomTypeId });
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdAsync(ShowTimeId1))
            .ReturnsAsync(new ShowTime
            {
                Id = ShowTimeId1,
                StartTime = new DateTime(2026, 3, 2, 19, 0, 0),
                ProjectionForm = ProjectionForm.ThreeD,
            });
        _uowMock.Setup(u => u.RoomTypeStore.GetByIdAsync(roomTypeId))
            .ReturnsAsync(new RoomType { Id = roomTypeId, Name = "IMAX", SupportsThreeD = true, ThreeDSurcharge = 40 });
        _uowMock.Setup(u => u.HolidayStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Holiday, bool>>>()))
            .ReturnsAsync(new List<Holiday>());
        _uowMock.Setup(u => u.TimeSlotStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TimeSlot, bool>>>()))
            .ReturnsAsync(new List<TimeSlot> { new() { Id = timeSlotId, TheaterId = theaterId, StartTime = "18:00", EndTime = "22:00" } });
        _uowMock.Setup(u => u.TicketPriceStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TicketPrice, bool>>>()))
            .ReturnsAsync(new List<TicketPrice> { new() { SeatTypeId = SeatTypeId1, TimeSlotId = timeSlotId, IsHoliday = false, Price = 250 } });

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId1));

        // The room class sets the base (matrix row 250), the dimension is charged on top (+40).
        result.Results.First().Price.Should().Be(290);
    }

    [Fact]
    public async Task GetSeatsAsync_SkipsThreeDSurcharge_WhenScreeningIsTwoD()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var seatType   = new SeatType { Id = SeatTypeId1, PriceMultiplier = 2 };
        var seats      = new List<Seat> { new() { Id = SeatId1, RowName = "A", ColIndex = 1, SeatTypeId = SeatTypeId1, SeatType = seatType } };

        _uowMock.Setup(u => u.SeatStore.GetByRoomAsync(RoomId1)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = ShowTimeId1, RoomId = RoomId1, BasePrice = 100 });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(RoomId1))
            .ReturnsAsync(new Room { Id = RoomId1, TheaterId = theaterId, RoomTypeId = roomTypeId });
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdAsync(ShowTimeId1))
            .ReturnsAsync(new ShowTime
            {
                Id = ShowTimeId1,
                StartTime = new DateTime(2026, 3, 2, 19, 0, 0),
                ProjectionForm = ProjectionForm.TwoD,
            });
        _uowMock.Setup(u => u.RoomTypeStore.GetByIdAsync(roomTypeId))
            .ReturnsAsync(new RoomType { Id = roomTypeId, Name = "IMAX", SupportsThreeD = true, ThreeDSurcharge = 40 });
        _uowMock.Setup(u => u.HolidayStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Holiday, bool>>>()))
            .ReturnsAsync(new List<Holiday>());
        _uowMock.Setup(u => u.TimeSlotStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TimeSlot, bool>>>()))
            .ReturnsAsync(new List<TimeSlot>());

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId1));

        // A 2D screening in an IMAX room pays the IMAX base only: 100 × 2, no surcharge.
        result.Results.First().Price.Should().Be(200);
        _uowMock.Verify(u => u.RoomTypeStore.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public void LockSeat_SeatBecomesLocked()
    {
        _sut.LockSeat(ShowTimeId1, RoomId1, SeatId10, "conn-abc");

        _sut.IsSeatLocked(ShowTimeId1, RoomId1, SeatId10).Should().BeTrue();
    }

    [Fact]
    public void UnlockSeat_SeatBecomesUnlocked()
    {
        _sut.LockSeat(ShowTimeId1, RoomId1, SeatId11, "conn-xyz");
        _sut.UnlockSeat(ShowTimeId1, RoomId1, SeatId11, "conn-xyz");

        _sut.IsSeatLocked(ShowTimeId1, RoomId1, SeatId11).Should().BeFalse();
    }

    [Fact]
    public void IsSeatLocked_ExcludeOwnConnection_ReturnsFalse()
    {
        _sut.LockSeat(ShowTimeId2, RoomId1, SeatId20, "my-conn");

        _sut.IsSeatLocked(ShowTimeId2, RoomId1, SeatId20, excludeConnectionId: "my-conn").Should().BeFalse();
    }

    [Fact]
    public void IsSeatLocked_OtherConnection_ReturnsTrue()
    {
        _sut.LockSeat(ShowTimeId2, RoomId1, SeatId21, "other-conn");

        _sut.IsSeatLocked(ShowTimeId2, RoomId1, SeatId21, excludeConnectionId: "my-conn").Should().BeTrue();
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsSeatHeldByAnotherConnection()
    {
        var userId   = Guid.NewGuid();
        var heldSeat = Guid.NewGuid();   // unique id so the static lock store isn't shared with other tests
        _sut.LockSeat(ShowTimeId1, RoomId1, heldSeat, "other-conn");

        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = ShowTimeId1, RoomId = RoomId1, BasePrice = 100 });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Room?)null);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());

        var request = new CreateBookingRequest
        {
            ShowTimeId    = ShowTimeId1,
            RoomId        = RoomId1,
            Seats         = new List<BookingSeatItem> { new() { SeatId = heldSeat } },
            ConnectionId  = "my-conn",           // booker's own connection differs from the holder's
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(userId, request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*held by another user*");
    }

    [Fact]
    public async Task CancelBookingAsync_WrongUser_ReturnsFalse()
    {
        var invoice = new Invoice
        {
            Id     = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = InvoiceStatus.Pending
        };
        var otherUserId = Guid.NewGuid();
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await _sut.CancelBookingAsync(otherUserId, invoice.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelBookingAsync_AlreadyPaid_ReturnsFalse()
    {
        var userId  = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id     = Guid.NewGuid(),
            UserId = userId,
            Status = InvoiceStatus.Paid
        };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await _sut.CancelBookingAsync(userId, invoice.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelBookingAsync_ValidPending_CancelsAndReturnsTrue()
    {
        var userId  = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id     = Guid.NewGuid(),
            UserId = userId,
            Status = InvoiceStatus.Pending
        };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.CancelBookingAsync(userId, invoice.Id);

        result.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    [Fact]
    public async Task RefundBookingAsync_NotPaid_ReturnsFalse()
    {
        var userId  = Guid.NewGuid();
        var invoice = new Invoice { Id = Guid.NewGuid(), UserId = userId, Status = InvoiceStatus.Pending };
        _uowMock.Setup(u => u.InvoiceStore.GetWithDetailsAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await _sut.RefundBookingAsync(userId, invoice.Id, isAdmin: false);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RefundBookingAsync_WrongUserNonAdmin_ReturnsFalse()
    {
        var invoice = new Invoice { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = InvoiceStatus.Paid };
        _uowMock.Setup(u => u.InvoiceStore.GetWithDetailsAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await _sut.RefundBookingAsync(Guid.NewGuid(), invoice.Id, isAdmin: false);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RefundBookingAsync_ValidPaid_RefundsReversesPointsAndSetsRefunded()
    {
        var userId  = Guid.NewGuid();
        var user    = new User { Id = userId, Email = "u@cinema.vn", Points = 50 };
        var invoice = new Invoice
        {
            Id               = Guid.NewGuid(),
            UserId           = userId,
            Status           = InvoiceStatus.Paid,
            FinalAmount      = 100000,          // originally accrued 10 points (1 / 10,000 VND)
            PaymentMethod    = "Sandbox",
            PaymentReference = "SANDBOX-abc",
            User             = user,
        };
        _uowMock.Setup(u => u.InvoiceStore.GetWithDetailsAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(user)).ReturnsAsync(user);
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<MemberShip, bool>>>()))
            .ReturnsAsync(new List<MemberShip>());
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.RefundBookingAsync(userId, invoice.Id, isAdmin: false);

        result.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Refunded);
        invoice.RefundedAt.Should().NotBeNull();
        user.Points.Should().Be(40);
    }

    [Fact]
    public async Task CancelBookingAsync_RestoresReservedLoyaltyPoints()
    {
        var userId  = Guid.NewGuid();
        var user    = new User { Id = userId, Points = 5 };
        var invoice = new Invoice
        {
            Id             = Guid.NewGuid(),
            UserId         = userId,
            Status         = InvoiceStatus.Pending,
            PointsRedeemed = 10,
        };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(user);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(user)).ReturnsAsync(user);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.CancelBookingAsync(userId, invoice.Id);

        result.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        user.Points.Should().Be(15);
    }

    [Fact]
    public async Task CancelBookingAsync_RestoresGiftCardBalance()
    {
        var userId  = Guid.NewGuid();
        var card    = new GiftCard { Id = Guid.NewGuid(), Balance = 20000 };
        var invoice = new Invoice
        {
            Id             = Guid.NewGuid(),
            UserId         = userId,
            Status         = InvoiceStatus.Pending,
            GiftCardId     = card.Id,
            GiftCardAmount = 30000,
        };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.GiftCardStore.GetByIdAsync(card.Id)).ReturnsAsync(card);
        _uowMock.Setup(u => u.GiftCardStore.UpdateAsync(card)).ReturnsAsync(card);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        await _sut.CancelBookingAsync(userId, invoice.Id);

        card.Balance.Should().Be(50000); // 20000 remaining + 30000 restored
    }

    [Fact]
    public async Task CancelBookingAsync_DeactivatesTicketsToFreeSeats()
    {
        var userId  = Guid.NewGuid();
        var invoice = new Invoice { Id = Guid.NewGuid(), UserId = userId, Status = InvoiceStatus.Pending };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        await _sut.CancelBookingAsync(userId, invoice.Id);

        // Frees the seats at the DB unique-index level for multi-instance safety.
        _uowMock.Verify(u => u.InvoiceStore.DeactivateTicketsAsync(invoice.Id), Times.Once);
    }
}
