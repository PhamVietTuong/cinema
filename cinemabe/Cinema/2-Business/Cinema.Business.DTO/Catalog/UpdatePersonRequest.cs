using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class UpdatePersonRequest : IHasId
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
}
