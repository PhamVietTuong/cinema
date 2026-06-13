namespace Cinema.Data.Entities;
public class SeatTypeTicketType
{
    public Guid SeatTypeId { get; set; }
    public Guid TicketTypeId { get; set; }
    public double PriceMultiplier { get; set; } = 1;
    public SeatType SeatType { get; set; } = null!;
    public TicketType TicketType { get; set; } = null!;
}
