using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Business.DTO.CustomerService;
using Cinema.Business.DTO.Invoices;
using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Managers;
using Cinema.Business.Security;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class CustomerServiceTests
{
    private const string _pin = "4321";

    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly Mock<IBoxOfficeManager> _boxOffice = new();
    private readonly Mock<IGiftCardManager> _giftCards = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<ISmsNotificationService> _sms = new();
    private readonly List<AuditLog> _staged = new();
    private readonly Guid _theaterId = Guid.NewGuid();
    private readonly User _staff;
    private readonly User _manager;
    private readonly User _customer;
    private readonly CustomerServiceManager _service;

    public CustomerServiceTests()
    {
        _uowMock.Setup(u => u.AuditLogStore.Stage(It.IsAny<AuditLog>())).Callback<AuditLog>(row => _staged.Add(row));
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(It.IsAny<User>())).ReturnsAsync((User user) => user);
        _uowMock.Setup(u => u.UserStore.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>())).ReturnsAsync(new Dictionary<Guid, string>());
        _uowMock.Setup(u => u.ComplaintStore.UpdateAsync(It.IsAny<Complaint>())).ReturnsAsync((Complaint c) => c);
        _uowMock.Setup(u => u.ComplaintStore.CreateAsync(It.IsAny<Complaint>())).ReturnsAsync((Complaint c) => c);
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MemberShip, bool>>>()))
            .ReturnsAsync(new List<MemberShip>());

        _staff = NewUser(RoleNames.TheaterStaff);
        _manager = NewUser(RoleNames.Admin);
        PasswordHasher.CreateHash(_pin, out var hash, out var salt);
        _manager.OverridePinHash = hash;
        _manager.OverridePinSalt = salt;
        _customer = NewUser(RoleNames.Customer);
        _customer.Email = "lan@example.com";
        _customer.Phone = "0900000123";
        _customer.TheaterId = null;

        var audit = new AuditLogger(_uowMock.Object);
        _service = new CustomerServiceManager(_uowMock.Object, audit, new ManagerOverrideService(_uowMock.Object, audit),
            _boxOffice.Object, _giftCards.Object, _notifications.Object, _sms.Object, TimeProvider.System);
    }

    private User NewUser(string role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = role,
            Status = UserStatus.Active,
            TheaterId = _theaterId,
            UserType = new UserType { Id = Guid.NewGuid(), Name = role }
        };
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(user.Id)).ReturnsAsync(user);
        return user;
    }

    private Complaint GivenComplaint(Guid? invoiceId = null, bool withCustomer = true, Guid? theaterId = null)
    {
        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            TheaterId = theaterId ?? _theaterId,
            InvoiceId = invoiceId,
            CustomerUserId = withCustomer ? _customer.Id : null,
            Description = "Projector broke",
            CreatedByUserId = _staff.Id
        };
        _uowMock.Setup(u => u.ComplaintStore.GetByIdAsync(complaint.Id)).ReturnsAsync(complaint);
        return complaint;
    }

    private void GivenResendInvoice(Guid invoiceId, string? email, string? phone, Guid? theaterId = null)
    {
        _uowMock.Setup(u => u.CustomerServiceStore.GetResendInvoiceAsync(invoiceId)).ReturnsAsync(
            new ResendInvoiceRow(invoiceId, "INV-9", theaterId ?? _theaterId, InvoiceStatus.Paid, 120000, email, phone, new List<string> { "QR1" }));
    }

    private void GivenRecentResends(Guid invoiceId, int count)
    {
        _uowMock.Setup(u => u.CustomerServiceStore.CountRecentAuditsAsync(invoiceId, AuditAction.ResendTicket, It.IsAny<DateTime>())).ReturnsAsync(count);
    }

    private ManagerOverrideDTO ManagerPin()
    {
        return new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin };
    }

    // ── Lookup ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lookup_ByPhone_FindsMember_MasksContact_AndOnlyAsksForInvoicesInScope()
    {
        var scope = new[] { _theaterId };
        _uowMock.Setup(u => u.CustomerServiceStore.FindCustomerByPhoneAsync("0900000123", RoleNames.Customer))
            .ReturnsAsync(new CustomerRow(_customer.Id, "Lan", "lan@example.com", "0900000123", "Gold", 450));
        var invoice = new CustomerInvoiceRow(Guid.NewGuid(), "INV-1", _theaterId, "Cinema A", InvoiceStatus.Paid, SalesChannel.Counter, 90000, DateTime.UtcNow, DateTime.UtcNow, "Dune", DateTime.Now, 2);
        _uowMock.Setup(u => u.CustomerServiceStore.GetInvoicesAsync(_customer.Id, null, scope, 20)).ReturnsAsync(new List<CustomerInvoiceRow> { invoice });

        var result = await _service.LookupCustomerAsync(scope, new LookupCustomerRequest { Query = " 0900000123 " });

        result.Found.Should().BeTrue();
        result.Customer!.Name.Should().Be("Lan");
        result.Customer.MembershipName.Should().Be("Gold");
        result.Customer.Points.Should().Be(450);
        result.Customer.MaskedEmail.Should().Be("l***@example.com");
        result.Customer.MaskedPhone.Should().Be("*******123");
        result.Invoices.Should().ContainSingle().Which.Code.Should().Be("INV-1");
        _uowMock.Verify(u => u.CustomerServiceStore.GetInvoicesAsync(_customer.Id, null, scope, 20), Times.Once);
    }

    [Fact]
    public async Task Lookup_ByEmail_UsesTheEmailQuery()
    {
        _uowMock.Setup(u => u.CustomerServiceStore.FindCustomerByEmailAsync("lan@example.com", RoleNames.Customer))
            .ReturnsAsync(new CustomerRow(_customer.Id, "Lan", "lan@example.com", "0900000123", null, 0));
        _uowMock.Setup(u => u.CustomerServiceStore.GetInvoicesAsync(It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), 20))
            .ReturnsAsync(new List<CustomerInvoiceRow>());

        var result = await _service.LookupCustomerAsync(null, new LookupCustomerRequest { Query = "lan@example.com" });

        result.Found.Should().BeTrue();
        _uowMock.Verify(u => u.CustomerServiceStore.FindCustomerByPhoneAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Lookup_UnknownWalkInInvoiceCode_ReturnsTheInvoiceWithoutMember()
    {
        var scope = new[] { _theaterId };
        var invoice = new CustomerInvoiceRow(Guid.NewGuid(), "INV-77", _theaterId, "Cinema A", InvoiceStatus.Paid, SalesChannel.Counter, 50000, null, DateTime.UtcNow, null, null, 1);
        _uowMock.Setup(u => u.CustomerServiceStore.GetInvoicesAsync(null, "INV-77", scope, 20)).ReturnsAsync(new List<CustomerInvoiceRow> { invoice });

        var result = await _service.LookupCustomerAsync(scope, new LookupCustomerRequest { Query = "INV-77" });

        result.Found.Should().BeTrue();
        result.Customer.Should().BeNull();
        result.Invoices.Should().ContainSingle();
    }

    // ── E-ticket resend ──────────────────────────────────────────────────────

    [Fact]
    public async Task Resend_ByEmail_SendsTheNotification_AndAudits()
    {
        var invoiceId = Guid.NewGuid();
        GivenResendInvoice(invoiceId, "lan@example.com", "0900000123");
        GivenRecentResends(invoiceId, 0);

        var result = await _service.ResendETicketAsync(_theaterId, _staff.Id, new ResendETicketRequest { InvoiceId = invoiceId, Channel = ETicketChannel.Email });

        _notifications.Verify(n => n.SendAsync("lan@example.com", It.Is<string>(s => s.Contains("INV-9")), It.Is<string>(b => b.Contains("QR1"))), Times.Once);
        _sms.Verify(s => s.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        result.RemainingThisHour.Should().Be(2);
        _staged.Should().ContainSingle().Which.Action.Should().Be(AuditAction.ResendTicket);
        _staged[0].EntityId.Should().Be(invoiceId);
        _staged[0].ActorUserId.Should().Be(_staff.Id);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Resend_BySms_UsesTheSmsService()
    {
        var invoiceId = Guid.NewGuid();
        GivenResendInvoice(invoiceId, "lan@example.com", "0900000123");
        GivenRecentResends(invoiceId, 2);

        var result = await _service.ResendETicketAsync(_theaterId, _staff.Id, new ResendETicketRequest { InvoiceId = invoiceId, Channel = ETicketChannel.Sms });

        _sms.Verify(s => s.SendSmsAsync("0900000123", It.Is<string>(m => m.Contains("INV-9"))), Times.Once);
        _notifications.Verify(n => n.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        result.RemainingThisHour.Should().Be(0);
    }

    [Fact]
    public async Task Resend_TheFourthTimeInAnHour_IsRefusedWith400_AndSendsNothing()
    {
        var invoiceId = Guid.NewGuid();
        GivenResendInvoice(invoiceId, "lan@example.com", null);
        GivenRecentResends(invoiceId, 3);

        var act = () => _service.ResendETicketAsync(_theaterId, _staff.Id, new ResendETicketRequest { InvoiceId = invoiceId, Channel = ETicketChannel.Email });

        await act.Should().ThrowAsync<InvalidOperationException>();
        _notifications.Verify(n => n.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _staged.Should().BeEmpty();
    }

    [Fact]
    public async Task Resend_WalkInInvoice_NeedsAnAddress_ThenUsesIt()
    {
        var invoiceId = Guid.NewGuid();
        GivenResendInvoice(invoiceId, null, null);
        GivenRecentResends(invoiceId, 0);

        var missing = () => _service.ResendETicketAsync(_theaterId, _staff.Id, new ResendETicketRequest { InvoiceId = invoiceId, Channel = ETicketChannel.Email });
        await missing.Should().ThrowAsync<InvalidOperationException>();

        await _service.ResendETicketAsync(_theaterId, _staff.Id, new ResendETicketRequest { InvoiceId = invoiceId, Channel = ETicketChannel.Email, Address = "walkin@example.com" });
        _notifications.Verify(n => n.SendAsync("walkin@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Resend_InvoiceOfAnotherTheater_Is403()
    {
        var invoiceId = Guid.NewGuid();
        GivenResendInvoice(invoiceId, "lan@example.com", null, theaterId: Guid.NewGuid());

        var act = () => _service.ResendETicketAsync(_theaterId, _staff.Id, new ResendETicketRequest { InvoiceId = invoiceId, Channel = ETicketChannel.Email });

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    // ── Complaint workflow ───────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithInvoice_TakesTheInvoiceCustomer_AndRefusesAnotherTheatersInvoice()
    {
        var invoiceId = Guid.NewGuid();
        _uowMock.Setup(u => u.CustomerServiceStore.GetInvoiceHeaderAsync(invoiceId))
            .ReturnsAsync(new InvoiceHeaderRow(invoiceId, "INV-5", _theaterId, _customer.Id, InvoiceStatus.Paid, 100000));
        Complaint? created = null;
        _uowMock.Setup(u => u.ComplaintStore.CreateAsync(It.IsAny<Complaint>())).Callback<Complaint>(c => created = c).ReturnsAsync((Complaint c) => c);

        var dto = await _service.CreateComplaintAsync(_theaterId, _staff.Id, new CreateComplaintRequest { Category = ComplaintCategory.Projection, Description = " Bad picture ", InvoiceId = invoiceId });

        created!.CustomerUserId.Should().Be(_customer.Id);
        created.CreatedByUserId.Should().Be(_staff.Id);
        created.Description.Should().Be("Bad picture");
        dto.Status.Should().Be(ComplaintStatus.Open);

        var otherId = Guid.NewGuid();
        _uowMock.Setup(u => u.CustomerServiceStore.GetInvoiceHeaderAsync(otherId))
            .ReturnsAsync(new InvoiceHeaderRow(otherId, "INV-6", Guid.NewGuid(), null, InvoiceStatus.Paid, 1));
        var act = () => _service.CreateComplaintAsync(_theaterId, _staff.Id, new CreateComplaintRequest { Description = "x", InvoiceId = otherId });
        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task Review_ThenReject_FollowsTheFlow_AndAClosedComplaintCannotBeTouched()
    {
        var complaint = GivenComplaint();

        var reviewed = await _service.StartComplaintReviewAsync(new[] { _theaterId }, _staff.Id, new StartComplaintReviewRequest { ComplaintId = complaint.Id });
        reviewed.Status.Should().Be(ComplaintStatus.InReview);
        reviewed.AssignedToUserId.Should().Be(_staff.Id);

        var again = () => _service.StartComplaintReviewAsync(new[] { _theaterId }, _staff.Id, new StartComplaintReviewRequest { ComplaintId = complaint.Id });
        await again.Should().ThrowAsync<InvalidOperationException>();

        var rejected = await _service.RejectComplaintAsync(new[] { _theaterId }, _staff.Id, new RejectComplaintRequest { ComplaintId = complaint.Id, Reason = "Not our fault" });
        rejected.Status.Should().Be(ComplaintStatus.Rejected);
        rejected.ResolvedByUserId.Should().Be(_staff.Id);

        var closed = () => _service.UpdateComplaintAsync(null, new UpdateComplaintRequest { ComplaintId = complaint.Id, Description = "x" });
        await closed.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Compensation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_GiftCard_ByManager_IssuesCardToCustomerEmail_AndAuditsCompensation()
    {
        var complaint = GivenComplaint();
        _giftCards.Setup(g => g.IssueAsync(It.IsAny<IssueGiftCardRequest>()))
            .ReturnsAsync(new GiftCardDTO { Id = Guid.NewGuid(), Code = "GC-1234", InitialBalance = 50000, Balance = 50000 });

        var dto = await _service.ResolveComplaintAsync(new[] { _theaterId }, _manager.Id,
            new ResolveComplaintRequest { ComplaintId = complaint.Id, Resolution = ComplaintResolution.GiftCard, Amount = 50000, Note = "Sorry" });

        _giftCards.Verify(g => g.IssueAsync(It.Is<IssueGiftCardRequest>(r => r.Amount == 50000 && r.IssuedToEmail == "lan@example.com")), Times.Once);
        dto.Status.Should().Be(ComplaintStatus.Resolved);
        dto.Resolution.Should().Be(ComplaintResolution.GiftCard);
        dto.CompensationRef.Should().Be("GC-1234");
        dto.CompensationAmount.Should().Be(50000);
        var audit = _staged.Should().ContainSingle().Subject;
        audit.Action.Should().Be(AuditAction.Compensation);
        audit.ActorUserId.Should().Be(_manager.Id);
        audit.ApproverUserId.Should().Be(_manager.Id);
        audit.EntityId.Should().Be(complaint.Id);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task Resolve_Points_AddsPoints_AndAuditsPointsAdjustAndCompensation()
    {
        var complaint = GivenComplaint();
        _customer.Points = 100;

        var dto = await _service.ResolveComplaintAsync(null, _staff.Id,
            new ResolveComplaintRequest { ComplaintId = complaint.Id, Resolution = ComplaintResolution.Points, Amount = 250, Override = ManagerPin() });

        _customer.Points.Should().Be(350);
        dto.Resolution.Should().Be(ComplaintResolution.Points);
        _staged.Select(a => a.Action).Should().BeEquivalentTo(new[] { AuditAction.PointsAdjust, AuditAction.Compensation });
        _staged.Should().OnlyContain(a => a.ActorUserId == _staff.Id && a.ApproverUserId == _manager.Id);
        _staged.Single(a => a.Action == AuditAction.PointsAdjust).Amount.Should().Be(250);
    }

    [Fact]
    public async Task Resolve_Refund_ByManager_DelegatesToStaffRefund_WithoutForwardingAPin()
    {
        var invoiceId = Guid.NewGuid();
        var complaint = GivenComplaint(invoiceId);
        _boxOffice.Setup(b => b.StaffRefundAsync(_theaterId, _manager.Id, It.IsAny<StaffRefundRequest>()))
            .ReturnsAsync(new StaffRefundResultDTO { InvoiceId = invoiceId, InvoiceCode = "INV-5", RefundedAmount = 120000, RefundTender = PaymentTender.Cash });

        var dto = await _service.ResolveComplaintAsync(null, _manager.Id,
            new ResolveComplaintRequest { ComplaintId = complaint.Id, Resolution = ComplaintResolution.Refund, RefundTender = PaymentTender.Cash });

        _boxOffice.Verify(b => b.StaffRefundAsync(_theaterId, _manager.Id, It.Is<StaffRefundRequest>(r =>
            r.InvoiceId == invoiceId && r.ReasonCode == StaffReasonCode.Compensation && r.Override == null && r.RefundTender == PaymentTender.Cash)), Times.Once);
        dto.CompensationAmount.Should().Be(120000);
        dto.CompensationRef.Should().Be("INV-5");
        _staged.Should().ContainSingle().Which.Action.Should().Be(AuditAction.Compensation);
    }

    [Fact]
    public async Task Resolve_Refund_ByStaffWithOverride_ForwardsTheOverride()
    {
        var invoiceId = Guid.NewGuid();
        var complaint = GivenComplaint(invoiceId);
        _boxOffice.Setup(b => b.StaffRefundAsync(_theaterId, _staff.Id, It.IsAny<StaffRefundRequest>()))
            .ReturnsAsync(new StaffRefundResultDTO { InvoiceId = invoiceId, InvoiceCode = "INV-5", RefundedAmount = 1000 });
        var pin = ManagerPin();

        await _service.ResolveComplaintAsync(null, _staff.Id,
            new ResolveComplaintRequest { ComplaintId = complaint.Id, Resolution = ComplaintResolution.Refund, Override = pin });

        _boxOffice.Verify(b => b.StaffRefundAsync(_theaterId, _staff.Id, It.Is<StaffRefundRequest>(r => r.Override == pin)), Times.Once);
        _staged.Single().ApproverUserId.Should().Be(_manager.Id);
    }

    [Fact]
    public async Task Resolve_Apology_NeedsNoApproval()
    {
        var complaint = GivenComplaint();

        var dto = await _service.ResolveComplaintAsync(null, _staff.Id,
            new ResolveComplaintRequest { ComplaintId = complaint.Id, Resolution = ComplaintResolution.Apology, Note = "Sorry" });

        dto.Status.Should().Be(ComplaintStatus.Resolved);
        _staged.Should().ContainSingle().Which.ApproverUserId.Should().BeNull();
        _giftCards.Verify(g => g.IssueAsync(It.IsAny<IssueGiftCardRequest>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_Compensation_ByNonApproverWithoutOverride_Is403_AndChangesNothing()
    {
        var complaint = GivenComplaint(Guid.NewGuid());

        foreach (var resolution in new[] { ComplaintResolution.Refund, ComplaintResolution.GiftCard, ComplaintResolution.Points })
        {
            var act = () => _service.ResolveComplaintAsync(null, _staff.Id,
                new ResolveComplaintRequest { ComplaintId = complaint.Id, Resolution = resolution, Amount = 10 });
            await act.Should().ThrowAsync<AccessDeniedException>();
        }

        complaint.Status.Should().Be(ComplaintStatus.Open);
        _giftCards.Verify(g => g.IssueAsync(It.IsAny<IssueGiftCardRequest>()), Times.Never);
        _boxOffice.Verify(b => b.StaffRefundAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<StaffRefundRequest>()), Times.Never);
        _uowMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task Resolve_ComplaintOutsideScope_Is403()
    {
        var complaint = GivenComplaint(theaterId: Guid.NewGuid());

        var act = () => _service.ResolveComplaintAsync(new[] { _theaterId }, _manager.Id,
            new ResolveComplaintRequest { ComplaintId = complaint.Id, Resolution = ComplaintResolution.Apology });
        await act.Should().ThrowAsync<AccessDeniedException>();

        var get = () => _service.GetComplaintAsync(new[] { _theaterId }, complaint.Id);
        _uowMock.Setup(u => u.ComplaintStore.GetRowAsync(complaint.Id)).ReturnsAsync(new ComplaintRow(complaint, null, null));
        await get.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task Resolve_GiftCardOrPoints_WithoutCustomerOrAmount_Is400()
    {
        var anonymous = GivenComplaint(withCustomer: false);
        var linked = GivenComplaint();

        var noCustomer = () => _service.ResolveComplaintAsync(null, _manager.Id,
            new ResolveComplaintRequest { ComplaintId = anonymous.Id, Resolution = ComplaintResolution.GiftCard, Amount = 100 });
        await noCustomer.Should().ThrowAsync<InvalidOperationException>();

        var noAmount = () => _service.ResolveComplaintAsync(null, _manager.Id,
            new ResolveComplaintRequest { ComplaintId = linked.Id, Resolution = ComplaintResolution.Points });
        await noAmount.Should().ThrowAsync<InvalidOperationException>();

        var noInvoice = () => _service.ResolveComplaintAsync(null, _manager.Id,
            new ResolveComplaintRequest { ComplaintId = linked.Id, Resolution = ComplaintResolution.Refund });
        await noInvoice.Should().ThrowAsync<InvalidOperationException>();
    }
}
