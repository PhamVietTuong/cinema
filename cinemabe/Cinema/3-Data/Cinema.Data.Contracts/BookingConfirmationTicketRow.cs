namespace Cinema.Data.Contracts;

public sealed class BookingConfirmationTicketRow
{
    public string SeatLabel { get; set; } = string.Empty;
    public string? PatronCategoryName { get; set; }
    public string? QrCode { get; set; }
}
