using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.BoxOffice;

public class CounterSaleRequest
{
    /// <summary>Required for admins (and multi-theater roles); staff may omit it.</summary>
    public Guid? TheaterId { get; set; }

    /// <summary>Required when Seats is not empty.</summary>
    public Guid? ShowTimeId { get; set; }

    /// <summary>Required when Seats is not empty.</summary>
    public Guid? RoomId { get; set; }

    /// <summary>Optional: a sale may be food and drinks only.</summary>
    public List<CounterSeatItem> Seats { get; set; } = new();

    public List<CounterFoodItem> Foods { get; set; } = new();

    /// <summary>Attached member (earns/spends points). Null = walk-in.</summary>
    public Guid? CustomerUserId { get; set; }

    [StringLength(64)]
    public string? DiscountCode { get; set; }

    [StringLength(64)]
    public string? GiftCardCode { get; set; }

    [Range(0, int.MaxValue)]
    public int PointsToRedeem { get; set; }

    /// <summary>What was paid. Ignored by Quote.</summary>
    public List<TenderLine> Tenders { get; set; } = new();

    /// <summary>The seat-lock hub connection id of the terminal, so its own held seats pass and are released.</summary>
    [StringLength(128)]
    public string? ConnectionId { get; set; }

    /// <summary>Manager approval, required when any line carries an OverrideUnitPrice.</summary>
    public ManagerOverrideDTO? Override { get; set; }

    /// <summary>The food is not handed over at the counter: it joins the kitchen's pickup queue (Pending) instead of
    /// being recorded as handed over. Ignored when the sale has no food.</summary>
    public bool HoldForPickup { get; set; }
}
