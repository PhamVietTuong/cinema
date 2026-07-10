namespace Cinema.Business.DTO.Booking;
public class TicketValidationDTO
{
    public bool Valid { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public string SeatLabel { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime ShowTime { get; set; }
    public string Message { get; set; } = string.Empty;
}
