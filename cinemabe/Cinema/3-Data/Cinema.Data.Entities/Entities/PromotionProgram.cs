using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>
/// A customer-facing promotion program (Cinestar-style "C'MONDAY", "Happy Hour", etc). Drives the
/// CinemaUser promotions page as display content, and lowers the price charged at checkout for
/// matching, in-window bookings (see BookingManager.ComputePricingAsync) — either automatically
/// (<see cref="AutoApply"/>) or when the customer enters its <see cref="Code"/>. A program is
/// triggered by exactly one of Code or AutoApply, never both (see PromotionProgramManager.ValidateAsync).
/// Formerly two separate entities (this one and the flat-percent Discount); merged so there is a single
/// pricing path and a single admin screen.
/// </summary>
public class PromotionProgram : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? BannerUrl { get; set; }

    /// <summary>Optional campaign family this program belongs to (e.g. "C'Student", "Happy Hour") —
    /// customer-facing grouping on the Promotions page filter. Replaced the old fixed
    /// Promotion/Seasonal/Membership enum, which only ever had three buckets and mostly funneled
    /// everything into "Promotion"; a managed, growable list of named campaigns is the useful grouping
    /// in practice. Null = no campaign (not shown under any filter tab but "All").</summary>
    public Guid? CampaignId { get; set; }
    public PromotionCampaign? Campaign { get; set; }

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
    /// eligible, in-window bookings — see BookingManager. Mutually exclusive with <see cref="Code"/>:
    /// a program is triggered by exactly one of the two.</summary>
    public bool AutoApply { get; set; }

    /// <summary>Optional promo code the customer enters at checkout. Mutually exclusive with
    /// <see cref="AutoApply"/>. Null/empty for an auto-applied or info-only program. Unique when present
    /// (multiple NULLs allowed).</summary>
    public string? Code { get; set; }
    /// <summary>Caps how many times this program's Code can be used in total; null = unlimited.</summary>
    public int? MaxUsage { get; set; }
    public int UsedCount { get; set; }
    /// <summary>Caps the summed per-unit saving (post-ComputeProgramSaving, pre-membership-stack);
    /// null = no cap.</summary>
    public double? MaxDiscountAmount { get; set; }
    /// <summary>Optional movie scope; null = applies to any movie. Theater/day/time scope is expressed
    /// via <see cref="Schedules"/> instead.</summary>
    public Guid? MovieId { get; set; }

    public MemberShip? MinMemberShip { get; set; }
    public Movie? Movie { get; set; }
    public ICollection<PromotionProgramPrice> Prices { get; set; } = new List<PromotionProgramPrice>();
    public ICollection<PromotionProgramSchedule> Schedules { get; set; } = new List<PromotionProgramSchedule>();
}
