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

[Trait("Req", "BR-PAY-01")]
[Trait("Req", "BR-PAY-03")]
[Trait("Req", "BR-PAY-05")]
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
    public async Task HandlePaymentCallbackAsync_ValidPayment_CountsThePromotionUseWithAnAtomicIncrement()
    {
        var invoice = PendingInvoice();
        var programId = Guid.NewGuid();
        invoice.PromotionProgramId = programId;
        _uowMock.Setup(u => u.PromotionProgramStore.IncrementUsedCountAsync(programId)).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        await _sut.HandlePaymentCallbackAsync("VNPay", VnPayTestHelper.SignedIpn(invoice.Id, 10_000_000));

        _uowMock.Verify(u => u.PromotionProgramStore.IncrementUsedCountAsync(programId), Times.Once);
        _uowMock.Verify(u => u.PromotionProgramStore.UpdateAsync(It.IsAny<PromotionProgram>()), Times.Never);
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

    private InvoiceTicket PaidTicket(bool isActive = true, bool isUsed = false)
    {
        var invoice = PendingInvoice();
        invoice.Status = InvoiceStatus.Paid;
        var ticket = new InvoiceTicket
        {
            InvoiceId = invoice.Id,
            Invoice = invoice,
            SeatId = Guid.NewGuid(),
            ShowTimeId = Guid.NewGuid(),
            IsActive = isActive,
            IsUsed = isUsed,
        };
        _uowMock.Setup(u => u.InvoiceStore.GetTicketByQrAsync("QR")).ReturnsAsync(ticket);
        return ticket;
    }

    [Fact]
    public async Task ValidateTicketAsync_DeactivatedTicket_IsRejected_EvenOnAPaidInvoice()
    {
        // A16: a cancelled booking's ticket is inactive; the Admin check-in must not admit it.
        PaidTicket(isActive: false);

        await _sut.Invoking(s => s.ValidateTicketAsync("QR", Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*no longer valid*");
        _uowMock.Verify(u => u.InvoiceStore.TryAdmitTicketAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task ValidateTicketAsync_AdmitsThroughTheAtomicUpdate_StampingTheAdmin()
    {
        var ticket = PaidTicket();
        var adminId = Guid.NewGuid();
        _uowMock.Setup(u => u.InvoiceStore.TryAdmitTicketAsync(
            ticket.InvoiceId, ticket.SeatId, ticket.ShowTimeId, adminId, It.IsAny<DateTime>())).ReturnsAsync(true);

        var result = await _sut.ValidateTicketAsync("QR", adminId);

        result.Valid.Should().BeTrue();
        _uowMock.Verify(u => u.InvoiceStore.TryAdmitTicketAsync(
            ticket.InvoiceId, ticket.SeatId, ticket.ShowTimeId, adminId, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task ValidateTicketAsync_LosingTheAdmitRace_ReportsAlreadyUsed()
    {
        PaidTicket();
        _uowMock.Setup(u => u.InvoiceStore.TryAdmitTicketAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>())).ReturnsAsync(false);

        await _sut.Invoking(s => s.ValidateTicketAsync("QR", Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already been used*");
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_TwoConcurrentDeliveries_ApplyTheSideEffectsExactlyOnce()
    {
        // B9: both deliveries read the invoice as Pending and reach the atomic claim at the same time; only one may
        // win it, so exactly one payment row and one loyalty accrual result, and the loser reports AlreadyConfirmed.
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Points = 0 };
        var invoice = PendingInvoice();
        invoice.UserId = userId;
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(user);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(user)).ReturnsAsync(user);
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MemberShip, bool>>>()))
            .ReturnsAsync(new List<MemberShip>());
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.InvoiceStore.GetStatusAsync(invoice.Id)).ReturnsAsync(InvoiceStatus.Paid);

        var arrivals = 0;
        var bothArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var claims = 0;
        _uowMock.Setup(u => u.InvoiceStore.TryTransitionStatusAsync(invoice.Id, InvoiceStatus.Pending, InvoiceStatus.Paid))
            .Returns(async () =>
            {
                if (Interlocked.Increment(ref arrivals) == 2)
                {
                    bothArrived.TrySetResult();
                }
                await bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(5));                      // hold both callers at the claim until both are there
                return Interlocked.Increment(ref claims) == 1;   // the database lets exactly one UPDATE change the row
            });
        var callback = VnPayTestHelper.SignedIpn(invoice.Id, 10_000_000);

        var outcomes = await Task.WhenAll(
            _sut.HandlePaymentCallbackAsync("VNPay", callback),
            _sut.HandlePaymentCallbackAsync("VNPay", callback));

        outcomes.Select(o => o.Outcome).Should().BeEquivalentTo(
            new[] { PaymentCallbackOutcome.Confirmed, PaymentCallbackOutcome.AlreadyConfirmed });
        invoice.Payments.Should().HaveCount(1);
        user.Points.Should().Be(100);   // 100,000 VND at the default 1,000 VND per point, accrued once
    }

    [Fact]
    public async Task ExpireStalePendingBookingsAsync_RestoresRedeemedPoints_AndFreesTheSeats()
    {
        // B8: a booking that times out gives back what it reserved; before this was only the food stock that was tested.
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Points = 10 };
        var invoice = PendingInvoice();
        invoice.UserId = userId;
        invoice.PointsRedeemed = 500;
        invoice.CreationTime = DateTime.UtcNow.AddMinutes(-20);
        _uowMock.Setup(u => u.InvoiceStore.GetStalePendingAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<Invoice> { invoice });
        _uowMock.Setup(u => u.InvoiceStore.TryTransitionStatusAsync(invoice.Id, InvoiceStatus.Pending, InvoiceStatus.Cancelled)).ReturnsAsync(true);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(It.IsAny<Invoice>())).ReturnsAsync((Invoice i) => i);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(user);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(user)).ReturnsAsync(user);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.StockMovementStore.GetNetSaleQuantitiesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<StockNetQuantity>());

        var count = await _sut.ExpireStalePendingBookingsAsync(TimeSpan.FromMinutes(15));

        count.Should().Be(1);
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        user.Points.Should().Be(510);
        _uowMock.Verify(u => u.InvoiceStore.DeactivateTicketsAsync(invoice.Id), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task InitiatePaymentAsync_ByANonOwner_ReturnsNull_AndTouchesNothing()
    {
        var invoice = PendingInvoice();
        invoice.UserId = Guid.NewGuid();
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await _sut.InitiatePaymentAsync(Guid.NewGuid(), invoice.Id, "Sandbox", null, null);

        result.Should().BeNull();
        _uowMock.Verify(u => u.InvoiceStore.UpdateAsync(It.IsAny<Invoice>()), Times.Never);
    }

    [Theory]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Cancelled)]
    [InlineData(InvoiceStatus.Failed)]
    public async Task InitiatePaymentAsync_OnANonPendingInvoice_ReturnsNull(InvoiceStatus status)
    {
        var userId = Guid.NewGuid();
        var invoice = PendingInvoice();
        invoice.UserId = userId;
        invoice.Status = status;
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await _sut.InitiatePaymentAsync(userId, invoice.Id, "Sandbox", null, null);

        result.Should().BeNull();
        _uowMock.Verify(u => u.InvoiceStore.UpdateAsync(It.IsAny<Invoice>()), Times.Never);
    }

    [Fact]
    public async Task InitiatePaymentAsync_ZeroAmountDue_FinalizesWithoutAnyGateway()
    {
        var userId = Guid.NewGuid();
        var invoice = PendingInvoice(finalAmount: 0);
        invoice.UserId = userId;
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(new User { Id = userId });
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MemberShip, bool>>>()))
            .ReturnsAsync(new List<MemberShip>());
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.InitiatePaymentAsync(userId, invoice.Id, "VNPay", null, null);

        result.Should().NotBeNull();
        result!.AlreadyPaid.Should().BeTrue();
        result.Provider.Should().Be("None");
        result.PaymentReference.Should().Be("ZERO-AMOUNT").And.NotContainEquivalentOf("gift");
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.Payments.Should().BeEmpty("nothing was charged");
    }

    [Fact]
    public async Task InitiatePaymentAsync_RecordsTheResolvedGatewayAsTheInvoiceOwner()
    {
        var userId = Guid.NewGuid();
        var invoice = PendingInvoice();
        invoice.UserId = userId;
        invoice.PaymentMethod = "VNPay";
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.InitiatePaymentAsync(userId, invoice.Id, "Sandbox", null, null);

        result!.Provider.Should().Be("Sandbox");
        invoice.PaymentMethod.Should().Be("Sandbox");   // later callbacks must come from this provider (A1)
        invoice.PaymentReference.Should().StartWith("SANDBOX-");
    }

    [Fact]
    public async Task HandlePaymentCallbackAsync_Sandbox_MissingInvoiceId_ReturnsRejected()
    {
        var handling = await _sut.HandlePaymentCallbackAsync("Sandbox", new Dictionary<string, string>());

        handling.Provider.Should().Be("Sandbox");
        handling.Outcome.Should().Be(PaymentCallbackOutcome.Rejected);
    }
}
