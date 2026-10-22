
namespace Cinema.Business.DTO.Catalog;

public class CreateAgeRestrictionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinAge { get; set; }
}
