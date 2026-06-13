using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class DiscountDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Percent { get; set; }
    public double? MaxDiscountAmount { get; set; }
    public Guid DiscountTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxUsage { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; }
}

public class CreateDiscountRequest
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Percent { get; set; }
    public double? MaxDiscountAmount { get; set; }
    public Guid DiscountTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxUsage { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateDiscountRequest : IHasId
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Percent { get; set; }
    public double? MaxDiscountAmount { get; set; }
    public Guid DiscountTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxUsage { get; set; }
    public bool IsActive { get; set; } = true;
}
