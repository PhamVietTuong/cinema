namespace Cinema.Data.Contracts;

/// <summary>One upcoming active ticket that a blocked seat/room affects, flattened for the relocation list.</summary>
public record AffectedTicketRow(
    Guid InvoiceId,
    string InvoiceCode,
    string CustomerName,
    string CustomerPhone,
    Guid ShowTimeId,
    Guid RoomId,
    string RoomName,
    string MovieTitle,
    DateTime StartTime,
    Guid SeatId,
    string SeatRowName,
    int SeatColIndex);
