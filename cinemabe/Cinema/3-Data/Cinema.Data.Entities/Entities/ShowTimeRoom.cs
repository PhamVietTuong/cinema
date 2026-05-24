namespace Cinema.Data.Entities;
public class ShowTimeRoom
{
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public int BasePrice { get; set; }
    public ShowTime ShowTime { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public ICollection<InvoiceTicket> InvoiceTickets { get; set; } = new List<InvoiceTicket>();
}
