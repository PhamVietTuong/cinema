namespace Cinema.Data.Entities;
public class InvoiceTicket
{
    public Guid InvoiceId { get; set; }
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public Guid SeatId { get; set; }
    public Guid TicketTypeId { get; set; }
    public decimal Price { get; set; }
    public string? QrCode { get; set; }
    public bool IsUsed { get; set; } = false;
    public Invoice Invoice { get; set; } = null!;
    public ShowTimeRoom ShowTimeRoom { get; set; } = null!;
    public Seat Seat { get; set; } = null!;
    public TicketType TicketType { get; set; } = null!;
}
