namespace Cinema.Data.Enums;

/// <summary>Customer-facing grouping for a PromotionProgram card, carried over from the old
/// DiscountType reference table — small and stable enough to be an enum instead of a CRUD screen
/// (exactly 3 real values in seed data: Khuyến Mãi/Promotion, Theo Mùa/Seasonal, Thành Viên/Membership).</summary>
public enum PromotionCategory
{
    Promotion = 0,
    Seasonal = 1,
    Membership = 2,
}
