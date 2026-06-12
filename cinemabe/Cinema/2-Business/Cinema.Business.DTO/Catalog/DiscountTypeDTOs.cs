using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class DiscountTypeDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CreateDiscountTypeRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateDiscountTypeRequest : IHasId
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
