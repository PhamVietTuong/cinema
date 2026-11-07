namespace Cinema.Business.DTO.Booking;

/// <summary>Whether a promo code can be used for a booking, and what it would take off.</summary>
public class DiscountCodeValidationDTO
{
    public bool Valid { get; set; }

    /// <summary>What this code alone would take off the given total, per-ticket-unit (same basis as
    /// booking), capped by the program's MaxDiscountAmount.</summary>
    public double DiscountAmount { get; set; }

    public string? Message { get; set; }
}
