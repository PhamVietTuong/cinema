namespace Cinema.Data.Enums;

/// <summary>Why a stock quantity changed. The signed <c>Quantity</c> on the movement carries the direction.</summary>
public enum StockMovementType
{
    Receive = 0,
    Sale = 1,
    SaleReversal = 2,
    Adjust = 3,
    Waste = 4
}
