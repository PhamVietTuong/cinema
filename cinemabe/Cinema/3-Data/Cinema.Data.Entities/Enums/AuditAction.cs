namespace Cinema.Data.Enums;

/// <summary>What a sensitive staff action was. Stored as int in <c>AuditLog.Action</c>; never renumber.</summary>
public enum AuditAction
{
    Other = 0,
    /// <summary>A wrong PIN / refused approver while trying to authorise a sensitive action.</summary>
    OverrideFailed = 1,
    OverridePinChanged = 2,
    PriceOverride = 3,
    Refund = 4,
    Exchange = 5,
    Reprint = 6,
    VoidSale = 7,
    CashPayOut = 8,
    DrawerReconcile = 9,
    Compensation = 10,
    PointsAdjust = 11,
    ResendTicket = 12,
    BlockSeat = 13,
    BlockRoom = 14,
    TicketAdmitOverride = 15,
    /// <summary>An admin changed the theater assignments of a RegionalManager.</summary>
    UserTheatersChanged = 16
}
