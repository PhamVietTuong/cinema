using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Payments;
using Cinema.Business.Helpers;
using Cinema.Business.Managers;
using Cinema.Business.Payments;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

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
                        Body = "Hi {{CustomerName}}, movie {{MovieTitle}}, {{ShowTime}} seats {{SeatLabels}} total {{TotalAmount}} {{QrImages}}" },
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
            Tickets = new List<BookingConfirmationTicketRow>
            {
                new() { SeatLabel = "B3", PatronCategoryName = "Người Cao Tuổi", QrCode = "QR-1" },
                new() { SeatLabel = "B4", PatronCategoryName = "Người Cao Tuổi", QrCode = "QR-2" },
            },
            Foods = new List<BookingConfirmationFoodRow> { new() { Name = "Kẹo Dẻo Trái Cây", Quantity = 1 } },
        });

        var result = await _sut.ConfirmPaymentAsync(userId, invoice.Id, "REF-1");

        result.Should().BeTrue();
        _notifications.Verify(n => n.SendAsync("user@cinema.vn",
            It.Is<string>(s => s.Contains(invoice.Code)),
            It.Is<string>(b => b.Contains("Deadpool &amp; Wolverine") && b.Contains("B3") && b.Contains("160,000")
                && b.Contains("cid:qr-0@cinema") && b.Contains("cid:qr-1@cinema")),
            It.Is<IReadOnlyList<EmailInlineImage>>(images =>
                images.Count == 2
                && images[0].ContentId == "qr-0@cinema" && images[0].MediaType == "image/png"
                && images[0].Content.SequenceEqual(QrCodeImage.ToPng("QR-1", 6))
                && images[1].ContentId == "qr-1@cinema"
                && images[1].Content.SequenceEqual(QrCodeImage.ToPng("QR-2", 6)))), Times.Once);
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
        _notifications.Verify(n => n.SendAsync("user@cinema.vn", $"Booking confirmed — {invoice.Code}", It.IsAny<string>(),
            It.Is<IReadOnlyList<EmailInlineImage>>(images => images.Count == 0)), Times.Once);
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
        _notifications.Verify(n => n.SendAsync("user@cinema.vn", $"Booking confirmed — {invoice.Code}", It.IsAny<string>(),
            It.Is<IReadOnlyList<EmailInlineImage>>(images => images.Count == 0)), Times.Once);
    }
}
