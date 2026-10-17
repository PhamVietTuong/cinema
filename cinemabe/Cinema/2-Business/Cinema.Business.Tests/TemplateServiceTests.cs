using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Payments;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.Helpers;
using Cinema.Business.Managers;
using Cinema.Business.Payments;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class TemplateRendererTests
{
    [Fact]
    public void Render_ReplacesKnownTokens()
    {
        var result = TemplateRenderer.Render("Hello {{Name}}, code {{Code}}",
            new Dictionary<string, string> { ["Name"] = "An", ["Code"] = "ABC" });

        result.Should().Be("Hello An, code ABC");
    }

    [Fact]
    public void Render_TreatsWhitespaceInsideBracesAsTolerable()
    {
        var result = TemplateRenderer.Render("Hello {{ Name }}", new Dictionary<string, string> { ["Name"] = "An" });

        result.Should().Be("Hello An");
    }

    [Fact]
    public void Render_LeavesUnknownTokenVerbatim()
    {
        var result = TemplateRenderer.Render("Hello {{Unknown}}", new Dictionary<string, string>());

        result.Should().Be("Hello {{Unknown}}");
    }

    [Fact]
    public void Render_NullText_ReturnsEmpty()
    {
        TemplateRenderer.Render(null, new Dictionary<string, string>()).Should().BeEmpty();
    }

    [Fact]
    public void Render_HtmlEncodesValuesByDefault()
    {
        var result = TemplateRenderer.Render("{{Name}}", new Dictionary<string, string> { ["Name"] = "A & B" });

        result.Should().Be("A &amp; B");
    }

    [Fact]
    public void Render_PlainText_DoesNotEncodeWhenDisabled()
    {
        var result = TemplateRenderer.Render("{{Name}}", new Dictionary<string, string> { ["Name"] = "A & B" }, encodeHtml: false);

        result.Should().Be("A & B");
    }
}

public class TemplateManagerTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly TemplateManager _sut;

    public TemplateManagerTests()
    {
        _sut = new TemplateManager(_uowMock.Object);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_Throws()
    {
        _uowMock.Setup(u => u.TemplateStore.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Template, bool>>>()))
            .ReturnsAsync(true);

        await FluentActions.Awaiting(() => _sut.CreateAsync(new CreateTemplateRequest
        {
            Code = "Dup", Name = "Dup", Channel = TemplateChannel.Sms, Body = "x"
        })).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_EmailWithoutSubject_Throws()
    {
        _uowMock.Setup(u => u.TemplateStore.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Template, bool>>>()))
            .ReturnsAsync(false);

        await FluentActions.Awaiting(() => _sut.CreateAsync(new CreateTemplateRequest
        {
            Code = "NewEmail", Name = "New", Channel = TemplateChannel.Email, Subject = null, Body = "x"
        })).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_SmsWithoutSubject_Succeeds()
    {
        _uowMock.Setup(u => u.TemplateStore.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Template, bool>>>()))
            .ReturnsAsync(false);
        _uowMock.Setup(u => u.TemplateStore.CreateAsync(It.IsAny<Template>())).ReturnsAsync((Template t) => t);

        var result = await _sut.CreateAsync(new CreateTemplateRequest
        {
            Code = "NewSms", Name = "New", Channel = TemplateChannel.Sms, Subject = null, Body = "x"
        });

        result.Code.Should().Be("NewSms");
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeCode()
    {
        var entity = new Template { Id = Guid.NewGuid(), Code = "Original", Name = "Old", Channel = TemplateChannel.Sms, Body = "old" };
        _uowMock.Setup(u => u.TemplateStore.GetByIdAsync(entity.Id)).ReturnsAsync(entity);
        _uowMock.Setup(u => u.TemplateStore.UpdateAsync(It.IsAny<Template>())).ReturnsAsync((Template t) => t);

        var result = await _sut.UpdateAsync(new UpdateTemplateRequest
        {
            Id = entity.Id, Name = "New Name", Channel = TemplateChannel.Sms, Body = "new body"
        });

        result.Code.Should().Be("Original");
        result.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateAsync_EmailWithoutSubject_Throws()
    {
        var entity = new Template { Id = Guid.NewGuid(), Code = "Original", Name = "Old", Channel = TemplateChannel.Email, Subject = "Old Subject", Body = "old" };
        _uowMock.Setup(u => u.TemplateStore.GetByIdAsync(entity.Id)).ReturnsAsync(entity);

        await FluentActions.Awaiting(() => _sut.UpdateAsync(new UpdateTemplateRequest
        {
            Id = entity.Id, Name = "New Name", Channel = TemplateChannel.Email, Subject = null, Body = "new body"
        })).Should().ThrowAsync<InvalidOperationException>();
    }
}

public class BookingConfirmationTemplateTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<ISmsNotificationService> _sms = new();
    private readonly Mock<ISeatNotificationService> _seatNotifications = new();
    private readonly BookingManager _sut;

    public BookingConfirmationTemplateTests()
    {
        var gateways = new PaymentGatewayResolver(new IPaymentGateway[] { new SandboxPaymentGateway() }, "Sandbox");
        _sut = new BookingManager(_uowMock.Object, gateways, _notifications.Object, _sms.Object, _seatNotifications.Object);
    }

    private static Invoice PendingInvoice(Guid userId)
    {
        return new Invoice
        {
            Id = Guid.NewGuid(),
            Code = "CIN2026101712375",
            UserId = userId,
            Status = InvoiceStatus.Pending,
            FinalAmount = 160000,
            TotalAmount = 160000,
        };
    }

    [Fact]
    public async Task ConfirmPayment_WithActiveTemplates_RendersBookingDataIntoNotifications()
    {
        var userId = Guid.NewGuid();
        var invoice = PendingInvoice(userId);
        var user = new User { Id = userId, Email = "user@cinema.vn", Phone = "0900000000", Name = "Nguyen Van A" };

        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(user);
        _uowMock.Setup(u => u.TemplateStore.GetActiveByCodesAsync(It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(new List<TemplateContentRow>
            {
                new() { Code = TemplateCodes.BookingConfirmationEmail, Channel = TemplateChannel.Email,
                        Subject = "Subj {{BookingCode}}",
                        Body = "Hi {{CustomerName}}, movie {{MovieTitle}}, {{ShowTime}} seats {{SeatLabels}} total {{TotalAmount}}" },
                new() { Code = TemplateCodes.BookingConfirmationSms, Channel = TemplateChannel.Sms, Subject = null,
                        Body = "SMS {{BookingCode}} {{MovieTitle}} {{SeatLabels}} {{TotalAmount}}" },
            });
        _uowMock.Setup(u => u.InvoiceStore.GetBookingConfirmationAsync(invoice.Id)).ReturnsAsync(new BookingConfirmationRow
        {
            MovieTitle = "Deadpool & Wolverine",
            TheaterName = "Cinema Landmark 81",
            RoomName = "Phòng 1",
            RoomTypeName = "Midnight Premium",
            ShowStartTime = new DateTime(2026, 10, 18, 6, 45, 0),
            ProjectionForm = ProjectionForm.TwoD,
            Tickets = new List<BookingConfirmationTicketRow> { new() { SeatLabel = "B3", PatronCategoryName = "Người Cao Tuổi", QrCode = "QR-1" } },
            Foods = new List<BookingConfirmationFoodRow> { new() { Name = "Kẹo Dẻo Trái Cây", Quantity = 1 } },
        });

        var result = await _sut.ConfirmPaymentAsync(userId, invoice.Id, "REF-1");

        result.Should().BeTrue();
        _notifications.Verify(n => n.SendAsync("user@cinema.vn",
            It.Is<string>(s => s.Contains(invoice.Code)),
            It.Is<string>(b => b.Contains("Deadpool &amp; Wolverine") && b.Contains("B3") && b.Contains("160,000"))), Times.Once);
        _sms.Verify(s => s.SendSmsAsync("0900000000",
            It.Is<string>(b => b.Contains("Deadpool & Wolverine") && b.Contains("B3"))), Times.Once);
    }

    [Fact]
    public async Task ConfirmPayment_TemplateLookupThrows_FallsBackAndStillSendsOnce()
    {
        var userId = Guid.NewGuid();
        var invoice = PendingInvoice(userId);
        var user = new User { Id = userId, Email = "user@cinema.vn", Phone = "0900000000", Name = "A" };

        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(user);
        _uowMock.Setup(u => u.TemplateStore.GetActiveByCodesAsync(It.IsAny<IReadOnlyCollection<string>>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _sut.ConfirmPaymentAsync(userId, invoice.Id, "REF-1");

        result.Should().BeTrue();
        _notifications.Verify(n => n.SendAsync("user@cinema.vn", $"Booking confirmed — {invoice.Code}", It.IsAny<string>()), Times.Once);
        _sms.Verify(s => s.SendSmsAsync("0900000000", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmPayment_NoActiveTemplates_FallsBackToHardcodedText()
    {
        var userId = Guid.NewGuid();
        var invoice = PendingInvoice(userId);
        var user = new User { Id = userId, Email = "user@cinema.vn", Phone = "0900000000", Name = "A" };

        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(user);
        _uowMock.Setup(u => u.TemplateStore.GetActiveByCodesAsync(It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(new List<TemplateContentRow>());

        var result = await _sut.ConfirmPaymentAsync(userId, invoice.Id, "REF-1");

        result.Should().BeTrue();
        _notifications.Verify(n => n.SendAsync("user@cinema.vn", $"Booking confirmed — {invoice.Code}", It.IsAny<string>()), Times.Once);
    }
}
