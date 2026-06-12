using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class TicketTypeDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string? Description { get; set; }
}

public class CreateTicketTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string? Description { get; set; }
}

public class UpdateTicketTypeRequest : IHasId
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string? Description { get; set; }
}
