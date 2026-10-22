using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class UpdateUserTypeRequest : IHasId
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
