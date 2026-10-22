
namespace Cinema.Business.DTO.Catalog;

public class CreateMemberShipRequest
{
    public string Name { get; set; } = string.Empty;
    public int MinPoints { get; set; }
    public int MaxPoints { get; set; }
    public double DiscountPercent { get; set; }
}
