namespace Cinema.Data.Enums;

/// <summary>
/// Pickup state of the food and drink on an invoice. Stored as int in <c>Invoice.FoodStatus</c>; never renumber.
/// A counter sale moves straight from Pending to HandedOver at the register; Preparing/Ready/Cancelled are vestigial
/// now that the kitchen pickup queue feature has been removed.
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
