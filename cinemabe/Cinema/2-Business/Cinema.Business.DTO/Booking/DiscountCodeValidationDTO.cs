namespace Cinema.Business.DTO.Booking;

/// <summary>Whether a promo code can be used for a booking, and what it would take off.</summary>
public class DiscountCodeValidationDTO
{
    public bool Valid { get; set; }

    /// <summary>What this code alone would take off the given total, per-ticket-unit (same basis as
    /// booking), capped by the program's MaxDiscountAmount.</summary>
    public double DiscountAmount { get; set; }

    /// <summary>True only when the theater's promotion cap actually reduced the saving; DiscountAmount is then the
    /// capped value and the customer sees a notice.</summary>
    public bool Capped { get; set; }

    /// <summary>The saving before the theater cap (equals DiscountAmount when not Capped).</summary>
    public double UncappedAmount { get; set; }

    /// <summary>The theater cap, as a percent of the summed ticket list prices; only meaningful when Capped (0 otherwise).</summary>
    public int CapPercent { get; set; }

    public string? Message { get; set; }
}
