namespace Cinema.Data.Enums;

/// <summary>Kind of cash movement in a drawer session. Stored as int; never renumber.</summary>
public enum CashMovementType
{
    OpeningFloat = 0,
    Sale = 1,
    Refund = 2,
    PayIn = 3,
    PayOut = 4
}
