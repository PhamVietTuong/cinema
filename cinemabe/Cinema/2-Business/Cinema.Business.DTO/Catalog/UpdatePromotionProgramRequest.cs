using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class UpdatePromotionProgramRequest : IHasId
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Summary { get; set; }
    public string? BannerUrl { get; set; }

    public PromotionMechanic Mechanic { get; set; }
    public double? PercentOff { get; set; }

    public PromotionEligibility Eligibility { get; set; }
    public Guid? MinMemberShipId { get; set; }
    public int? MaxAge { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool ExcludeHolidays { get; set; }
    public int? AllowedChannelsMask { get; set; }
    public int? MaxTicketsPerTransaction { get; set; }
    public bool StacksWithMembershipDiscount { get; set; }

    [StringLength(2000)]
    public string? TermsText { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public bool AutoApply { get; set; }
    public Guid? LinkedDiscountId { get; set; }

    public List<PromotionProgramPriceDTO> Prices { get; set; } = new();
    public List<PromotionProgramScheduleDTO> Schedules { get; set; } = new();
}
