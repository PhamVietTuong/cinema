using Cinema.Business.DTO.Booking;

namespace Cinema.Business.DTO.Requests;

public class ValidateDiscountCodeRequest
{
    public string Code { get; set; } = string.Empty;

    public Guid RoomId { get; set; }

    public Guid ShowTimeId { get; set; }

    public double Total { get; set; }

    /// <summary>The seats already chosen on the checkout page, so the server can compute the promo's
    /// per-ticket-unit saving the same way booking does. Empty for a food-only quote (saving is 0, but
    /// the code's existence/window/usage is still validated).</summary>
    public List<BookingSeatItem> Seats { get; set; } = new();
}
