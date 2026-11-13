using Cinema.Business.Managers;
using Cinema.Business.DTO.Catalog;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class InvoiceAdminManagerTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly InvoiceAdminManager _sut;

    public InvoiceAdminManagerTests()
    {
        _sut = new InvoiceAdminManager(_uowMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(It.IsAny<Invoice>())).ReturnsAsync((Invoice i) => i);
        _uowMock.Setup(u => u.InvoiceStore.TryTransitionStatusAsync(It.IsAny<Guid>(), It.IsAny<InvoiceStatus>(), It.IsAny<InvoiceStatus>()))
            .ReturnsAsync(true);
        _uowMock.Setup(u => u.StockMovementStore.GetNetSaleQuantitiesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<StockNetQuantity>());
    }

    private Invoice Setup(InvoiceStatus status, Guid? userId = null)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = status,
            FinalAmount = 200_000,
            FoodStatus = FoodOrderStatus.Pending,
        };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        return invoice;
    }

    [Fact]
    public async Task UpdateStatus_PendingToCancelled_FreesSeatsAndRestoresReservations()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Points = 10 };
        var invoice = Setup(InvoiceStatus.Pending, userId);
        invoice.PointsRedeemed = 500;
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(user);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(user)).ReturnsAsync(user);

        var dto = await _sut.UpdateStatusAsync(new UpdateInvoiceStatusRequest { Id = invoice.Id, Status = InvoiceStatus.Cancelled });

        dto.Status.Should().Be(InvoiceStatus.Cancelled);
        invoice.FoodStatus.Should().Be(FoodOrderStatus.Cancelled);
        user.Points.Should().Be(510);
        _uowMock.Verify(u => u.InvoiceStore.DeactivateTicketsAsync(invoice.Id), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_PaidToCancelled_DeactivatesTickets_ButDoesNotRefundPoints()
    {
        var invoice = Setup(InvoiceStatus.Paid, Guid.NewGuid());
        invoice.PointsRedeemed = 500;

        await _sut.UpdateStatusAsync(new UpdateInvoiceStatusRequest { Id = invoice.Id, Status = InvoiceStatus.Cancelled });

        _uowMock.Verify(u => u.InvoiceStore.DeactivateTicketsAsync(invoice.Id), Times.Once);
        _uowMock.Verify(u => u.UserStore.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatus_PendingToPaid_AddsPaymentRowAndAccruesLoyaltyOnce()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Points = 0 };
        var invoice = Setup(InvoiceStatus.Pending, userId);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(user);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(user)).ReturnsAsync(user);
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MemberShip, bool>>>()))
            .ReturnsAsync(new List<MemberShip>());

        await _sut.UpdateStatusAsync(new UpdateInvoiceStatusRequest { Id = invoice.Id, Status = InvoiceStatus.Paid });

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAt.Should().NotBeNull();
        invoice.Payments.Should().ContainSingle(p => p.Amount == 200_000);
        user.Points.Should().Be(200); // 200,000 VND at the default 1,000 VND per point
        _uowMock.Verify(u => u.InvoiceStore.DeactivateTicketsAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData(InvoiceStatus.Paid, InvoiceStatus.Pending)]
    [InlineData(InvoiceStatus.Cancelled, InvoiceStatus.Pending)]
    [InlineData(InvoiceStatus.Cancelled, InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Failed, InvoiceStatus.Paid)]
    public async Task UpdateStatus_ReopeningAClosedInvoice_Throws_NoWrites(InvoiceStatus from, InvoiceStatus to)
    {
        var invoice = Setup(from);

        await _sut.Invoking(s => s.UpdateStatusAsync(new UpdateInvoiceStatusRequest { Id = invoice.Id, Status = to }))
            .Should().ThrowAsync<InvalidOperationException>();
        invoice.Status.Should().Be(from);
        _uowMock.Verify(u => u.InvoiceStore.TryTransitionStatusAsync(It.IsAny<Guid>(), It.IsAny<InvoiceStatus>(), It.IsAny<InvoiceStatus>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatus_LosesTheClaim_ThrowsAndRollsBack()
    {
        var invoice = Setup(InvoiceStatus.Pending);
        _uowMock.Setup(u => u.InvoiceStore.TryTransitionStatusAsync(invoice.Id, InvoiceStatus.Pending, InvoiceStatus.Cancelled))
            .ReturnsAsync(false);

        await _sut.Invoking(s => s.UpdateStatusAsync(new UpdateInvoiceStatusRequest { Id = invoice.Id, Status = InvoiceStatus.Cancelled }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*changed*");
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.InvoiceStore.DeactivateTicketsAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Delete_PendingInvoice_RestoresPoints()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Points = 0 };
        var invoice = Setup(InvoiceStatus.Pending, userId);
        invoice.PointsRedeemed = 300;
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(user);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(user)).ReturnsAsync(user);

        await _sut.DeleteAsync(invoice.Id);

        user.Points.Should().Be(300);
        _uowMock.Verify(u => u.InvoiceStore.DeleteAsync(invoice.Id), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_PaidInvoice_DoesNotRestoreAnything()
    {
        var invoice = Setup(InvoiceStatus.Paid, Guid.NewGuid());
        invoice.PointsRedeemed = 300;

        await _sut.DeleteAsync(invoice.Id);

        _uowMock.Verify(u => u.UserStore.UpdateAsync(It.IsAny<User>()), Times.Never);
        _uowMock.Verify(u => u.InvoiceStore.DeleteAsync(invoice.Id), Times.Once);
    }
}
