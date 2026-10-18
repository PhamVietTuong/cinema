using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>Read-only projection of everything a booking-confirmation notification needs beyond
/// what's already on the tracked Invoice (movie/theater/room/showtime, tickets, food lines).</summary>
public sealed class BookingConfirmationRow
{
    public string? MovieTitle { get; set; }
    public string? TheaterName { get; set; }
    public string? RoomName { get; set; }
    public string? RoomTypeName { get; set; }
    public DateTime? ShowStartTime { get; set; }
    public ProjectionForm? ProjectionForm { get; set; }
    public List<BookingConfirmationTicketRow> Tickets { get; set; } = new();
    public List<BookingConfirmationFoodRow> Foods { get; set; } = new();
}

public sealed class BookingConfirmationTicketRow
{
    public string SeatLabel { get; set; } = string.Empty;
    public string? PatronCategoryName { get; set; }
    public string? QrCode { get; set; }
}

public sealed class BookingConfirmationFoodRow
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
