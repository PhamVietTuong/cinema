namespace Cinema.Data.Enums;

/// <summary>Where an invoice was sold. Stored as int in <c>Invoice.Channel</c>; never renumber.</summary>
public enum SalesChannel
{
    Online = 0,
    Counter = 1
}
