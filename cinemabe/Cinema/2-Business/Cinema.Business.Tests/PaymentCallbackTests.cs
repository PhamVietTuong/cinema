using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Payments;
using Cinema.Business.Managers;
using Cinema.Business.Notifications;
using Cinema.Business.Payments;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class PaymentCallbackTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly BookingManager _sut;

    public PaymentCallbackTests()
    {
        var gateways = new PaymentGatewayResolver(new IPaymentGateway[] { new SandboxPaymentGateway(), VnPayTestHelper.Gateway() }, "Sandbox");
        _sut = new BookingManager(_uowMock.Object, gateways, new DevLogNotificationService(), new DevLogSmsNotificationService(), new Mock<ISeatNotificationService>().Object);
        _uowMock.Setup(u => u.InvoiceStore.TryTransitionStatusAsync(It.IsAny<Guid>(), InvoiceStatus.Pending, It.IsAny<InvoiceStatus>())).ReturnsAsync(true);
    }

    private static Invoice PendingInvoice(decimal finalAmount = 100_000) => new()
    {
        Id            = Guid.NewGuid(),
        UserId        = null,
        DiscountId    = null,
        FoodStatus    = FoodOrderStatus.None,
        Status        = InvoiceStatus.Pending,
        PaymentMethod = "VNPay",
        FinalAmount   = (double)finalAmount,
    };

    [Fact]
    public async Task HandlePaymentCallbackAsync_ValidPayment_ConfirmsInvoiceAndFinalizes()
    {
        var invoice = PendingInvoice();
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        var callback = VnPayTestHelper.SignedIpn(invoice.Id, 10_000_000);

        var handling = await _sut.HandlePaymentCallbackAsync("VNPay", callback);

        handling.Provider.Should().Be("VNPay");
        handling.Outcome.Should().Be(PaymentCallbackOutcome.Confirmed);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaymentReference.Should().Be("13456789");
        invoice.PaidAt.Should().NotBeNull();
        invoice.Payments.Should().ContainSingle(p => p.Amount == 100_000);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_AlreadyPaidInvoice_ReturnsAlreadyConfirmed_NoWrites()
    {
        var invoice = PendingInvoice();
        invoice.Status = InvoiceStatus.Paid;
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        var callback = VnPayTestHelper.SignedIpn(invoice.Id, 10_000_000);

        var handling = await _sut.HandlePaymentCallbackAsync("VNPay", callback);

        handling.Outcome.Should().Be(PaymentCallbackOutcome.AlreadyConfirmed);
        _uowMock.Verify(u => u.InvoiceStore.UpdateAsync(It.IsAny<Invoice>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_InvoiceNotFound_ReturnsOrderNotFound()
    {
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Invoice?)null);
        var callback = VnPayTestHelper.SignedIpn(Guid.NewGuid(), 10_000_000);

        var handling = await _sut.HandlePaymentCallbackAsync("VNPay", callback);

        handling.Outcome.Should().Be(PaymentCallbackOutcome.OrderNotFound);
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_TamperedSignature_ReturnsInvalidSignature_NoInvoiceLookup()
    {
        var callback = new Dictionary<string, string>
        {
            ["vnp_Amount"] = "10000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TxnRef"] = Guid.NewGuid().ToString("N"),
            ["vnp_SecureHash"] = "not-a-valid-hash",
        };

        var handling = await _sut.HandlePaymentCallbackAsync("VNPay", callback);

        handling.Outcome.Should().Be(PaymentCallbackOutcome.InvalidSignature);
        _uowMock.Verify(u => u.InvoiceStore.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_AmountMismatch_ReturnsInvalidAmount_InvoiceStaysPending()
    {
        var invoice = PendingInvoice(finalAmount: 100_000);
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        // Invoice expects 100,000 VND; callback reports 50,000 VND (vnp_Amount is VND * 100).
        var callback = VnPayTestHelper.SignedIpn(invoice.Id, 5_000_000);

        var handling = await _sut.HandlePaymentCallbackAsync("VNPay", callback);

        handling.Outcome.Should().Be(PaymentCallbackOutcome.InvalidAmount);
        invoice.Status.Should().Be(InvoiceStatus.Pending);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_FailedPayment_AcknowledgesWithoutTouchingInvoice()
    {
        var invoice = PendingInvoice();
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        var callback = VnPayTestHelper.SignedIpn(invoice.Id, 10_000_000, responseCode: "24", transactionStatus: "02");

        var handling = await _sut.HandlePaymentCallbackAsync("VNPay", callback);

        handling.Outcome.Should().Be(PaymentCallbackOutcome.PaymentFailedAcknowledged);
        invoice.Status.Should().Be(InvoiceStatus.Pending);
        _uowMock.Verify(u => u.InvoiceStore.UpdateAsync(It.IsAny<Invoice>()), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_CancelledInvoice_ReturnsNotPending()
    {
        var invoice = PendingInvoice();
        invoice.Status = InvoiceStatus.Cancelled;
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        var callback = VnPayTestHelper.SignedIpn(invoice.Id, 10_000_000);

        var handling = await _sut.HandlePaymentCallbackAsync("VNPay", callback);

        handling.Outcome.Should().Be(PaymentCallbackOutcome.NotPending);
        _uowMock.Verify(u => u.InvoiceStore.UpdateAsync(It.IsAny<Invoice>()), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_Sandbox_ValidInvoiceId_Confirms()
    {
        var invoice = PendingInvoice();
        invoice.PaymentMethod = "Sandbox";
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        var callback = new Dictionary<string, string> { ["invoiceId"] = invoice.Id.ToString() };

        var handling = await _sut.HandlePaymentCallbackAsync("Sandbox", callback);

        handling.Provider.Should().Be("Sandbox");
        handling.Outcome.Should().Be(PaymentCallbackOutcome.Confirmed);
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_SandboxCallbackOnVnPayInvoice_IsRejected_NoWrites()
    {
        // A1: an unsigned Sandbox callback must not finalize an invoice owned by VNPay.
        var invoice = PendingInvoice();
        invoice.PaymentMethod = "VNPay";
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        var callback = new Dictionary<string, string> { ["invoiceId"] = invoice.Id.ToString() };

        var handling = await _sut.HandlePaymentCallbackAsync("Sandbox", callback);

        handling.Outcome.Should().NotBe(PaymentCallbackOutcome.Confirmed);
        invoice.Status.Should().Be(InvoiceStatus.Pending);
        _uowMock.Verify(u => u.InvoiceStore.UpdateAsync(It.IsAny<Invoice>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_InvoiceOwnedByUnavailableProvider_DoesNotFallBackToSandbox()
    {
        // A2: the client picked PaymentMethod freely; an unknown/unconfigured name resolves to the default
        // gateway (Sandbox here) and must not be allowed to approve the confirm.
        var userId = Guid.NewGuid();
        var invoice = PendingInvoice();
        invoice.UserId = userId;
        invoice.PaymentMethod = "SomethingElse";
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);

        var confirmed = await _sut.ConfirmPaymentAsync(userId, invoice.Id, "any-reference");

        confirmed.Should().BeFalse();
        invoice.Status.Should().Be(InvoiceStatus.Pending);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_LosesTheClaimRace_ReturnsAlreadyConfirmed_NoSideEffects()
    {
        // A3: a duplicate IPN read Pending, but the other delivery already flipped the row to Paid.
        var invoice = PendingInvoice();
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.TryTransitionStatusAsync(invoice.Id, InvoiceStatus.Pending, InvoiceStatus.Paid)).ReturnsAsync(false);
        _uowMock.Setup(u => u.InvoiceStore.GetStatusAsync(invoice.Id)).ReturnsAsync(InvoiceStatus.Paid);
        var callback = VnPayTestHelper.SignedIpn(invoice.Id, 10_000_000);

        var handling = await _sut.HandlePaymentCallbackAsync("VNPay", callback);

        handling.Outcome.Should().Be(PaymentCallbackOutcome.AlreadyConfirmed);
        invoice.Payments.Should().BeEmpty();
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_InvoiceCancelledDuringRace_ReturnsNotPending()
    {
        // A4: the expiry job cancelled the invoice between the IPN's read and its claim.
        var invoice = PendingInvoice();
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.TryTransitionStatusAsync(invoice.Id, InvoiceStatus.Pending, InvoiceStatus.Paid)).ReturnsAsync(false);
        _uowMock.Setup(u => u.InvoiceStore.GetStatusAsync(invoice.Id)).ReturnsAsync(InvoiceStatus.Cancelled);
        var callback = VnPayTestHelper.SignedIpn(invoice.Id, 10_000_000);

        var handling = await _sut.HandlePaymentCallbackAsync("VNPay", callback);

        handling.Outcome.Should().Be(PaymentCallbackOutcome.NotPending);
    }

    [Fact]
    public async Task ExpireStalePendingBookingsAsync_SkipsInvoiceThatWasJustPaid()
    {
        // A4: an IPN marked this invoice Paid after the reaper loaded it as Pending; it must not be cancelled.
        var paid = PendingInvoice();
        var stale = PendingInvoice();
        _uowMock.Setup(u => u.InvoiceStore.GetStalePendingAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Invoice> { paid, stale });
        _uowMock.Setup(u => u.InvoiceStore.TryTransitionStatusAsync(paid.Id, InvoiceStatus.Pending, InvoiceStatus.Cancelled)).ReturnsAsync(false);
        _uowMock.Setup(u => u.InvoiceStore.TryTransitionStatusAsync(stale.Id, InvoiceStatus.Pending, InvoiceStatus.Cancelled)).ReturnsAsync(true);
        _uowMock.Setup(u => u.StockMovementStore.GetNetSaleQuantitiesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<StockNetQuantity>());
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(It.IsAny<Invoice>())).ReturnsAsync((Invoice i) => i);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var count = await _sut.ExpireStalePendingBookingsAsync(TimeSpan.FromMinutes(15));

        count.Should().Be(1);
        paid.Status.Should().Be(InvoiceStatus.Pending);
        stale.Status.Should().Be(InvoiceStatus.Cancelled);
        _uowMock.Verify(u => u.InvoiceStore.DeactivateTicketsAsync(paid.Id), Times.Never);
        _uowMock.Verify(u => u.InvoiceStore.DeactivateTicketsAsync(stale.Id), Times.Once);
    }

    [Fact]
    public async Task ExpireStalePendingBookingsAsync_GivesAnInFlightPaymentTheExtraGraceWindow()
    {
        // A5: an invoice whose payment page was opened keeps its seats 15 minutes longer than an untouched one, so a
        // late IPN can still confirm it instead of arriving after the reaper cancelled it.
        var untouched = PendingInvoice();
        untouched.CreationTime = DateTime.UtcNow.AddMinutes(-20);
        var inFlight = PendingInvoice();
        inFlight.CreationTime = DateTime.UtcNow.AddMinutes(-20);
        inFlight.PaymentReference = "txn-in-flight";
        var longAbandoned = PendingInvoice();
        longAbandoned.CreationTime = DateTime.UtcNow.AddMinutes(-40);
        longAbandoned.PaymentReference = "txn-abandoned";
        _uowMock.Setup(u => u.InvoiceStore.GetStalePendingAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Invoice> { untouched, inFlight, longAbandoned });
        _uowMock.Setup(u => u.InvoiceStore.TryTransitionStatusAsync(It.IsAny<Guid>(), InvoiceStatus.Pending, InvoiceStatus.Cancelled)).ReturnsAsync(true);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(It.IsAny<Invoice>())).ReturnsAsync((Invoice i) => i);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.StockMovementStore.GetNetSaleQuantitiesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<StockNetQuantity>());

        var count = await _sut.ExpireStalePendingBookingsAsync(TimeSpan.FromMinutes(15));

        count.Should().Be(2);
        untouched.Status.Should().Be(InvoiceStatus.Cancelled);
        longAbandoned.Status.Should().Be(InvoiceStatus.Cancelled);
        inFlight.Status.Should().Be(InvoiceStatus.Pending);
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_Sandbox_MissingInvoiceId_ReturnsRejected()
    {
        var handling = await _sut.HandlePaymentCallbackAsync("Sandbox", new Dictionary<string, string>());

        handling.Provider.Should().Be("Sandbox");
        handling.Outcome.Should().Be(PaymentCallbackOutcome.Rejected);
    }
}
