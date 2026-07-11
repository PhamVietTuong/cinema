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
    public double Price { get; set; }
}

public class CreateTicketPriceRequest
{
    public Guid TheaterId { get; set; }
    public Guid RoomTypeId { get; set; }
    public Guid SeatTypeId { get; set; }
    public Guid TimeSlotId { get; set; }
    public bool IsHoliday { get; set; }
    public double Price { get; set; }
}

public class UpdateTicketPriceRequest : IHasId
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid RoomTypeId { get; set; }
    public Guid SeatTypeId { get; set; }
    public Guid TimeSlotId { get; set; }
    public bool IsHoliday { get; set; }
    public double Price { get; set; }
}
