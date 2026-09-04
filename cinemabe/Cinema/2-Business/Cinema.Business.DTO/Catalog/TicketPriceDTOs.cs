using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class TicketPriceDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid RoomTypeId { get; set; }
    public Guid SeatTypeId { get; set; }
    public Guid TimeSlotId { get; set; }
    public bool IsHoliday { get; set; }
    /// <summary>Factor applied to the showtime's BasePrice, not an absolute amount.</summary>
    public double PriceMultiplier { get; set; } = 1;
}

public class CreateTicketPriceRequest
{
    public Guid TheaterId { get; set; }
    public Guid RoomTypeId { get; set; }
    public Guid SeatTypeId { get; set; }
    public Guid TimeSlotId { get; set; }
    public bool IsHoliday { get; set; }
    /// <summary>Factor applied to the showtime's BasePrice, not an absolute amount.</summary>
    public double PriceMultiplier { get; set; } = 1;
}

public class UpdateTicketPriceRequest : IHasId
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid RoomTypeId { get; set; }
    public Guid SeatTypeId { get; set; }
    public Guid TimeSlotId { get; set; }
    public bool IsHoliday { get; set; }
    /// <summary>Factor applied to the showtime's BasePrice, not an absolute amount.</summary>
    public double PriceMultiplier { get; set; } = 1;
}
