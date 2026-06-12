using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class AgeRestrictionDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinAge { get; set; }
}

public class CreateAgeRestrictionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinAge { get; set; }
}

public class UpdateAgeRestrictionRequest : IHasId
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinAge { get; set; }
}
