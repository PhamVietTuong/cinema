namespace Cinema.Data.Enums;

/// <summary>How a PromotionProgram affects price. Info is display-only and never affects price;
/// FixedPrice and PercentOff may also auto-apply at checkout (see PromotionProgram.AutoApply).</summary>
public enum PromotionMechanic
{
    Info = 0,
    FixedPrice = 1,
    PercentOff = 2,
}
