namespace Cinema.Data.Enums;

/// <summary>Reportable reason for a manual Adjust/Waste movement (the free-text note stays in <c>Reason</c>).</summary>
public enum StockReasonCode
{
    Other = 0,
    Expired = 1,
    Damaged = 2,
    Spilled = 3,
    TheftOrLoss = 4,
    StockCountCorrection = 5,
    OpeningBalance = 6
}
