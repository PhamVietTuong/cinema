namespace Cinema.Data.Enums;

/// <summary>The dimension a sales report is grouped by. Part of the staff report API contract; never renumber.</summary>
public enum SalesGroupBy
{
    Day = 0,
    Movie = 1,
    Theater = 2,
    PaymentMethod = 3,
    Staff = 4,
    Channel = 5
}
