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

    /// <summary>Self-reported patron category for this seat (Adult/Student/Senior/Child); null = full
    /// price. Checked visually (ID/student card) at the theater, not verified by this system.
    /// The category's seat-type allow-list (PatronCategorySeatType) is enforced server-side only when
    /// this is set — omitting it books any seat type at full price. That's intentional: the gate is a
    /// pricing-category restriction, not a standalone access-control rule on the seat itself.</summary>
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
