namespace Cinema.Data.Entities;
public class TicketType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public double BasePrice { get; set; }
    public string? Description { get; set; }
    public ICollection<SeatTypeTicketType> SeatTypeTicketTypes { get; set; } = new List<SeatTypeTicketType>();
    public ICollection<InvoiceTicket> InvoiceTickets { get; set; } = new List<InvoiceTicket>();
}
