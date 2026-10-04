using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.BoxOffice;

public class CounterSeatItem
{
    [NotEmptyGuid]
    public Guid SeatId { get; set; }

    /// <summary>Patron category (ticket type) for this seat. Required, same rule as online booking.</summary>
    public Guid? PatronCategoryId { get; set; }

    /// <summary>Manager-approved price for this ticket row (a double seat is two rows). Needs a manager override;
    /// must be between 0 and the list price. Null = list price.</summary>
    [Range(0, 100000000)]
    public double? OverrideUnitPrice { get; set; }
}

public class CounterFoodItem
{
    [NotEmptyGuid]
    public Guid FoodAndDrinkId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }

    /// <summary>Manager-approved unit price. Needs a manager override; between 0 and the list price. Null = list price.</summary>
    [Range(0, 100000000)]
    public double? OverrideUnitPrice { get; set; }
}

/// <summary>One payment the customer hands over. Only Cash, Card and QrWallet are accepted here: points and gift cards
/// are applied through <see cref="CounterSaleRequest.PointsToRedeem"/> / <see cref="CounterSaleRequest.GiftCardCode"/>.</summary>
public class TenderLine
{
    public PaymentTender Method { get; set; }

    /// <summary>Cash: the amount handed over (change is computed). Card/QR: the amount charged on the terminal.</summary>
    [Range(0, 100000000)]
    public double Amount { get; set; }

    /// <summary>Terminal / wallet reference. Required for Card and QrWallet.</summary>
    [StringLength(100)]
    public string? Reference { get; set; }
}

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
}

public class CounterQuoteLineDTO
{
    /// <summary>"Seat" or "Food".</summary>
    public string Kind { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
    /// <summary>Price before any manager override.</summary>
    public double ListUnitPrice { get; set; }
    public double LineTotal { get; set; }
}

public class CounterQuoteDTO
{
    public List<CounterQuoteLineDTO> Lines { get; set; } = new();
    public double TotalAmount { get; set; }
    /// <summary>Membership and promo discounts only (points and gift card are reported separately).</summary>
    public double DiscountAmount { get; set; }
    public double PointsValue { get; set; }
    public double GiftCardAmount { get; set; }
    /// <summary>What remains to be tendered.</summary>
    public double FinalAmount { get; set; }
}

public class CounterSaleResultDTO
{
    public Guid InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public double TotalAmount { get; set; }
    /// <summary>Membership and promo discounts only.</summary>
    public double DiscountAmount { get; set; }
    public double PointsValue { get; set; }
    public double GiftCardAmount { get; set; }
    public double FinalAmount { get; set; }
    /// <summary>Cash to hand back to the customer.</summary>
    public double ChangeDue { get; set; }
    public DateTime PaidAt { get; set; }
    public List<CounterQuoteLineDTO> Lines { get; set; } = new();
    public List<TicketItemDTO> Tickets { get; set; } = new();
}

/// <summary>Everything the booking core needs to ring up a counter sale (resolved by <c>BoxOfficeManager</c>).</summary>
public class CounterSaleContext
{
    public Guid TheaterId { get; set; }
    public Guid StaffUserId { get; set; }
    /// <summary>The seller's open drawer in this theater, if any (required when cash is tendered).</summary>
    public Guid? CashDrawerSessionId { get; set; }
    /// <summary>Approver of the price overrides (the staff member themself when they are an approver).</summary>
    public Guid? ApproverUserId { get; set; }
    public CounterSaleRequest Request { get; set; } = new();
}

public class OpenDrawerRequest
{
    public Guid? TheaterId { get; set; }

    [Required]
    [StringLength(100)]
    public string TerminalName { get; set; } = string.Empty;

    [Range(0, 100000000)]
    public double OpeningFloat { get; set; }
}

public class BoxOfficeScopeRequest
{
    public Guid? TheaterId { get; set; }
}

public class PayInOutRequest
{
    public Guid? TheaterId { get; set; }

    /// <summary>PayIn or PayOut only.</summary>
    public CashMovementType Type { get; set; }

    [Range(1, 100000000)]
    public double Amount { get; set; }

    [Required]
    [StringLength(500)]
    public string Note { get; set; } = string.Empty;

    /// <summary>Required for a PayOut.</summary>
    public ManagerOverrideDTO? Override { get; set; }
}

public class CashMovementDTO
{
    public Guid Id { get; set; }
    public CashMovementType Type { get; set; }
    /// <summary>Signed: positive money in, negative money out.</summary>
    public double Amount { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? Note { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CashDrawerDTO
{
    /// <summary>False when the caller has no open drawer (all other fields are then empty).</summary>
    public bool IsOpen { get; set; }
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string TerminalName { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public double OpeningFloat { get; set; }
    public double CashSales { get; set; }
    public double PayIns { get; set; }
    public double PayOuts { get; set; }
    public double Refunds { get; set; }
    /// <summary>Opening float + sales + pay-ins - pay-outs - refunds.</summary>
    public double ExpectedCash { get; set; }
    /// <summary>Newest first (up to 20).</summary>
    public List<CashMovementDTO> RecentMovements { get; set; } = new();
}

public class ShowtimesTodayRequest
{
    public Guid? TheaterId { get; set; }

    /// <summary>Business day (server local time). Defaults to today.</summary>
    public DateTime? Date { get; set; }
}

public class CounterShowtimeDTO
{
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public Guid MovieId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ProjectionForm ProjectionForm { get; set; }
    /// <summary>The showtime has finished and can no longer be sold.</summary>
    public bool HasEnded { get; set; }
}

public class FindCustomerRequest
{
    [Required]
    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;
}

public class CounterCustomerDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int Points { get; set; }
    public string? MemberShipName { get; set; }
    public double DiscountPercent { get; set; }
}
