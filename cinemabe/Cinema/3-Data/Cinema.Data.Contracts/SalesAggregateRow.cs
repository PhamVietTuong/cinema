namespace Cinema.Data.Contracts;

/// <summary>
/// One group of a sales aggregate. Exactly one of the key fields is filled, depending on the dimension:
/// <see cref="DayKey"/> (Day), <see cref="GuidKey"/> (Theater, Staff, Movie) or <see cref="IntKey"/> (Channel,
/// PaymentMethod). Movie and PaymentMethod also yield one row with every key null: what no movie / no tender line
/// can claim, so the rows add up to the invoice totals. Measures that a dimension cannot attribute are left 0.
/// </summary>
public class SalesAggregateRow
{
    public DateTime? DayKey { get; set; }
    public Guid? GuidKey { get; set; }
    public int? IntKey { get; set; }
    public int InvoiceCount { get; set; }
    public double TicketAmount { get; set; }
    public double FoodAmount { get; set; }
    public double FinalAmount { get; set; }
    public double DiscountAmount { get; set; }
}
