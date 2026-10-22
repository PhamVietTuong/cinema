using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Booking;

public class TicketItemDTO
{
    public string SeatLabel { get; set; } = string.Empty;
    /// <summary>"Single" or "Double".</summary>
    public string SeatType { get; set; } = string.Empty;
    public double Price { get; set; }
    public string PatronCategory { get; set; } = string.Empty;
    public string? QrCode { get; set; }
}
