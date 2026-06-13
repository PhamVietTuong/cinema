using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class MemberShipDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinPoints { get; set; }
    public int MaxPoints { get; set; }
    public double DiscountPercent { get; set; }
}

public class CreateMemberShipRequest
{
    public string Name { get; set; } = string.Empty;
    public int MinPoints { get; set; }
    public int MaxPoints { get; set; }
    public double DiscountPercent { get; set; }
}

public class UpdateMemberShipRequest : IHasId
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinPoints { get; set; }
    public int MaxPoints { get; set; }
    public double DiscountPercent { get; set; }
}
