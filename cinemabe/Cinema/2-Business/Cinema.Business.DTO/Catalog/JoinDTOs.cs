namespace Cinema.Business.DTO.Catalog;

// ── MovieTypeDetail (Movie ↔ MovieType) ───────────────────────────────────────
public class MovieTypeDetailDTO
{
    public Guid MovieId { get; set; }
    public Guid MovieTypeId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string MovieTypeName { get; set; } = string.Empty;
}

public class CreateMovieTypeDetailRequest
{
    public Guid MovieId { get; set; }
    public Guid MovieTypeId { get; set; }
}

// ── SeatTypeTicketType (SeatType ↔ TicketType price matrix) ────────────────────
public class SeatTypeTicketTypeDTO
{
    public Guid SeatTypeId { get; set; }
    public Guid TicketTypeId { get; set; }
    public decimal PriceMultiplier { get; set; }
    public string SeatTypeName { get; set; } = string.Empty;
    public string TicketTypeName { get; set; } = string.Empty;
}

public class CreateSeatTypeTicketTypeRequest
{
    public Guid SeatTypeId { get; set; }
    public Guid TicketTypeId { get; set; }
    public decimal PriceMultiplier { get; set; } = 1;
}

public class UpdateSeatTypeTicketTypeRequest
{
    public Guid SeatTypeId { get; set; }
    public Guid TicketTypeId { get; set; }
    public decimal PriceMultiplier { get; set; } = 1;
}
