namespace Cinema.Data.Enums;

/// <summary>Lifecycle of a cashier's drawer session. Stored as int; never renumber.</summary>
public enum CashDrawerStatus
{
    Open = 0,
    Closed = 1,
    Reconciled = 2
}
