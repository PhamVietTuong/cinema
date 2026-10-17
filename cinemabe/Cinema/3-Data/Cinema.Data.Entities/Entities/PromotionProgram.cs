using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>
/// A customer-facing promotion program (Cinestar-style "C'MONDAY", "Happy Hour", etc). Drives the
/// CinemaUser promotions page as display content, and — only when <see cref="AutoApply"/> is true —
/// also lowers the price charged at checkout for matching, in-window bookings (see
/// BookingManager.ComputePricingAsync). It is deliberately a separate aggregate from <see cref="Discount"/>:
/// Discount stays the flat-percent, code/auto-apply engine already wired into checkout; this entity adds
/// fixed-price-by-format/seat-kind, eligibility and richer validity on top without touching that path.
/// </summary>
public class PromotionProgram : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? BannerUrl { get; set; }

    public PromotionMechanic Mechanic { get; set; } = PromotionMechanic.Info;
    /// <summary>Required when Mechanic = PercentOff (0,100]. Unused otherwise.</summary>
    public double? PercentOff { get; set; }

    public PromotionEligibility Eligibility { get; set; } = PromotionEligibility.Everyone;
    /// <summary>Required when Eligibility = Members: the minimum tier (by MinPoints) that qualifies.</summary>
    public Guid? MinMemberShipId { get; set; }
    /// <summary>Required when Eligibility = AgeLimit: the "under this age" threshold.</summary>
    public int? MaxAge { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Display-only term; also enforced as a real gate when AutoApply is true.</summary>
    public bool ExcludeHolidays { get; set; }
    /// <summary>Bitmask of SalesChannel values (bit n = channel n); null = all channels.</summary>
    public int? AllowedChannelsMask { get; set; }
    public int? MaxTicketsPerTransaction { get; set; }
    public bool StacksWithMembershipDiscount { get; set; }
    /// <summary>Free-text extra terms, one per line.</summary>
    public string? TermsText { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }

    /// <summary>When true, this program is evaluated automatically at checkout (no code entry) for
    /// eligible, in-window bookings — see BookingManager. Requires Mechanic != Info, Eligibility in
    /// (Everyone, Members), and LinkedDiscountId to be null (only one source of price per program).</summary>
    public bool AutoApply { get; set; }

    /// <summary>Optional: ties this program's card to an existing code-based Discount, shown on the
    /// CinemaUser card. Does not itself change price — only LinkedDiscount's own auto-apply/code
    /// behavior does, same as today.</summary>
    public Guid? LinkedDiscountId { get; set; }

    public MemberShip? MinMemberShip { get; set; }
    public Discount? LinkedDiscount { get; set; }
    public ICollection<PromotionProgramPrice> Prices { get; set; } = new List<PromotionProgramPrice>();
    public ICollection<PromotionProgramSchedule> Schedules { get; set; } = new List<PromotionProgramSchedule>();
}
