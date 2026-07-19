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
        _sut = new BookingManager(_uowMock.Object, gateways, new DevLogNotificationService());
    }

    private static PagingSearchDTO SeatSearch(Guid showTimeId, Guid roomId) => new()
    {
        Filters = new Dictionary<string, string>
        {
            ["showTimeId"] = showTimeId.ToString(),
            ["roomId"]     = roomId.ToString()
        }
    };

    [Fact]
    public async Task GetSeatsAsync_ReturnsAvailableStatus_WhenNotBookedOrLocked()
    {
        var seats = new List<Seat> { new() { Id = SeatId1, RowName = "A", ColIndex = 1, SeatTypeId = SeatTypeId1 } };
        _uowMock.Setup(u => u.SeatStore.GetByRoomAsync(RoomId1)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1))
            .ReturnsAsync(new ShowTimeRoom { BasePrice = 100 });

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

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId2));

        result.Results.First().Status.Should().Be(SeatStatus.Occupied);
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
}
