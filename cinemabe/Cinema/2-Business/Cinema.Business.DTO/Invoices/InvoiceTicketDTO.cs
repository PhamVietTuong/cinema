using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Invoices;

public class InvoiceTicketDTO
{
    public string MovieTitle { get; set; } = string.Empty;
    public string TheaterName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime ShowTime { get; set; }
    public string SeatLabel { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? PatronCategoryName { get; set; }
    public double PatronDiscountPercent { get; set; }
    public string? QrCode { get; set; }
    public bool IsUsed { get; set; }
}
