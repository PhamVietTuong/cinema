
namespace Cinema.Business.DTO.Catalog;

public class CreateDiscountRequest
{
    public string? Code { get; set; }
    public string? Description { get; set; }
    public double Percent { get; set; }
    public double? MaxDiscountAmount { get; set; }
    public Guid DiscountTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxUsage { get; set; }
    public bool IsActive { get; set; } = true;

    public bool AutoApply { get; set; }
    public bool ApplyToAllTheaters { get; set; } = true;
    public List<Guid> TheaterIds { get; set; } = new();
    public Guid? MovieId { get; set; }
    public int? DaysOfWeekMask { get; set; }
    public string? StartTimeOfDay { get; set; }
    public string? EndTimeOfDay { get; set; }
}
