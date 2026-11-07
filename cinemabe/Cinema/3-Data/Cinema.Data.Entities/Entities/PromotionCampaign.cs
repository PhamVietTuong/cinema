namespace Cinema.Data.Entities;

/// <summary>A named promotion campaign family (e.g. "C'Student", "C'Ten", "Happy Hour") that one or
/// more PromotionProgram rows belong to — the customer-facing filter on the Promotions page groups by
/// this instead of the old fixed Promotion/Seasonal/Membership enum, since in practice there are many
/// more than three meaningful groupings and the set keeps growing as new campaigns launch.</summary>
public class PromotionCampaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}
