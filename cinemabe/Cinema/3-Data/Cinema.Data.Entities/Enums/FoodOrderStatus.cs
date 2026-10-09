namespace Cinema.Data.Enums;

/// <summary>
/// Pickup state of the food and drink on an invoice. Stored as int in <c>Invoice.FoodStatus</c>; never renumber.
/// Allowed moves: Pending to Preparing to Ready to HandedOver, or Ready to HandedOver directly (see <c>IConcessionManager</c>).
/// </summary>
public enum FoodOrderStatus
{
    None = 0,
    Pending = 1,
    Preparing = 2,
    Ready = 3,
    HandedOver = 4,
    Cancelled = 5
}
