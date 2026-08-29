using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Requests;
using FluentAssertions;

namespace Cinema.Business.Tests;

/// <summary>
/// Covers the DataAnnotations on the request DTOs. [ApiController] runs these before an action body
/// executes, so they are the outermost guard on every endpoint — these tests assert the contract the
/// managers are then allowed to assume.
/// </summary>
public class DtoValidationTests
{
    private static IList<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    private static bool IsValid(object dto)
    {
        return Validate(dto).Count == 0;
    }

    // ── Register ──────────────────────────────────────────────────────────────

    private static RegisterRequest ValidRegistration()
    {
        return new RegisterRequest
        {
            Name            = "Nguyen Van A",
            Email           = "a@cinema.vn",
            Phone           = "0901234567",
            Password        = "Password@1",
            ConfirmPassword = "Password@1"
        };
    }

    [Fact]
    public void RegisterRequest_WellFormed_IsValid()
    {
        IsValid(ValidRegistration()).Should().BeTrue();
    }

    [Fact]
    public void RegisterRequest_MismatchedConfirmPassword_IsInvalid()
    {
        var request = ValidRegistration();
        request.ConfirmPassword = "SomethingElse@1";

        Validate(request).Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(RegisterRequest.ConfirmPassword));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void RegisterRequest_BadEmail_IsInvalid(string email)
    {
        var request = ValidRegistration();
        request.Email = email;

        IsValid(request).Should().BeFalse();
    }

    [Fact]
    public void RegisterRequest_ShortPassword_IsInvalid()
    {
        var request = ValidRegistration();
        request.Password        = "Pass@1";
        request.ConfirmPassword = "Pass@1";

        IsValid(request).Should().BeFalse();
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LoginRequest_ShortPassword_IsStillValid()
    {
        // Deliberate: length policy applies to register/change/reset, never to login. Enforcing it
        // here would 400 legacy passwords instead of 401-ing them.
        var request = new LoginRequest { EmailOrPhone = "a@cinema.vn", Password = "old" };

        IsValid(request).Should().BeTrue();
    }

    [Fact]
    public void LoginRequest_EmptyPassword_IsInvalid()
    {
        var request = new LoginRequest { EmailOrPhone = "a@cinema.vn", Password = "" };

        IsValid(request).Should().BeFalse();
    }

    // ── Two-factor ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("123456", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("abcdef", false)]
    public void VerifyTwoFactorRequest_CodeMustBeSixDigits(string code, bool expected)
    {
        var request = new VerifyTwoFactorRequest { EmailOrPhone = "a@cinema.vn", Code = code };

        IsValid(request).Should().Be(expected);
    }

    // ── Booking ───────────────────────────────────────────────────────────────

    private static CreateBookingRequest ValidBooking()
    {
        return new CreateBookingRequest
        {
            ShowTimeId    = Guid.NewGuid(),
            RoomId        = Guid.NewGuid(),
            Seats         = new List<BookingSeatItem> { new() { SeatId = Guid.NewGuid() } },
            PaymentMethod = "Sandbox"
        };
    }

    [Fact]
    public void CreateBookingRequest_WellFormed_IsValid()
    {
        IsValid(ValidBooking()).Should().BeTrue();
    }

    [Fact]
    public void CreateBookingRequest_NoSeats_IsInvalid()
    {
        var request = ValidBooking();
        request.Seats = new List<BookingSeatItem>();

        IsValid(request).Should().BeFalse();
    }

    [Fact]
    public void CreateBookingRequest_EmptyShowTimeId_IsInvalid()
    {
        var request = ValidBooking();
        request.ShowTimeId = Guid.Empty;

        IsValid(request).Should().BeFalse();
    }

    [Fact]
    public void CreateBookingRequest_NegativePointsToRedeem_IsInvalid()
    {
        var request = ValidBooking();
        request.PointsToRedeem = -500;

        IsValid(request).Should().BeFalse();
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(0)]
    public void BookingFoodItem_NonPositiveQuantity_IsInvalid(int quantity)
    {
        // A negative quantity would subtract from the order total in BookingManager's
        // `foodTotal += food.Price * f.Quantity`.
        var item = new BookingFoodItem { FoodAndDrinkId = Guid.NewGuid(), Quantity = quantity };

        IsValid(item).Should().BeFalse();
    }

    [Fact]
    public void BookingFoodItem_PositiveQuantity_IsValid()
    {
        var item = new BookingFoodItem { FoodAndDrinkId = Guid.NewGuid(), Quantity = 2 };

        IsValid(item).Should().BeTrue();
    }

    // ── Paging ────────────────────────────────────────────────────────────────

    [Fact]
    public void PagingSearchDTO_Defaults_AreValid()
    {
        IsValid(new PagingSearchDTO()).Should().BeTrue();
    }

    [Fact]
    public void PagingSearchDTO_ZeroPageSize_IsValid()
    {
        // The managers read `PageSize > 0 ? PageSize : 20`, so 0 must keep meaning "use the default".
        IsValid(new PagingSearchDTO { PageSize = 0 }).Should().BeTrue();
    }

    [Fact]
    public void PagingSearchDTO_OversizedPageSize_IsInvalid()
    {
        IsValid(new PagingSearchDTO { PageSize = 100_000 }).Should().BeFalse();
    }
}
