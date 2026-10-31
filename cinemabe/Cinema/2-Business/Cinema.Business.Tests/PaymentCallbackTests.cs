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
    public async Task HandlePaymentCallbackAsync_Sandbox_MissingInvoiceId_ReturnsRejected()
    {
        var handling = await _sut.HandlePaymentCallbackAsync("Sandbox", new Dictionary<string, string>());

        handling.Provider.Should().Be("Sandbox");
        handling.Outcome.Should().Be(PaymentCallbackOutcome.Rejected);
    }
}
