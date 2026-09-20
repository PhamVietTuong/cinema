using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Validation;

namespace Cinema.Business.DTO.Booking;
public class CreateBookingRequest
{
    [NotEmptyGuid]
    public Guid ShowTimeId { get; set; }

    [NotEmptyGuid]
    public Guid RoomId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "A booking must include at least one seat.")]
    public List<BookingSeatItem> Seats { get; set; } = new();

    public List<BookingFoodItem> Foods { get; set; } = new();

    [StringLength(64)]
    public string? DiscountCode { get; set; }

    [Required]
    [StringLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>Loyalty points the customer wants to spend on this booking (0 = none). Capped server-side
    /// at the balance and the order total.</summary>
    [Range(0, int.MaxValue)]
    public int PointsToRedeem { get; set; }

    /// <summary>The caller's SignalR connection id (from the seat-locking hub). When supplied, booking
    /// rejects seats another connection is actively holding; the caller's own held seats still pass.</summary>
    [StringLength(128)]
    public string? ConnectionId { get; set; }

    /// <summary>Optional gift-card code to apply its balance to this booking.</summary>
    [StringLength(64)]
    public string? GiftCardCode { get; set; }
}

public class BookingSeatItem
{
    [NotEmptyGuid]
    public Guid SeatId { get; set; }

    /// <summary>Self-reported patron category for this seat (Adult/Student/Senior/Child) — REQUIRED,
    /// since PatronCategory.Price is now the only source of a ticket's price. Checked visually
    /// (ID/student card) at the theater, not verified by this system. The category's own seat kind
    /// (Standard/Double) must match the seat being booked, or the booking is rejected — this is the
    /// entire eligibility rule (a category with no row for a kind simply cannot book that kind).</summary>
    public Guid? PatronCategoryId { get; set; }
}

public class BookingFoodItem
{
    [NotEmptyGuid]
    public Guid FoodAndDrinkId { get; set; }

    // Lower bound matters: BookingManager sums Price * Quantity, so a negative quantity would
    // subtract from the order total.
    [Range(1, 100)]
    public int Quantity { get; set; }
}
