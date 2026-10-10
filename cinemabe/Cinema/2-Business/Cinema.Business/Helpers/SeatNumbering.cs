using Cinema.Data.Entities;

namespace Cinema.Business.Helpers;

/// <summary>
/// Row-position seat numbering: within a row, a double/couple seat (two seats sharing a SeatGroupId,
/// column-adjacent, exactly 2 members in that row) counts as ONE numbered slot, so the seat after it
/// doesn't jump by two. Mirrors the frontend's seatCellsByRow grouping rule and the SQL backfill in
/// upgrade_db.sql — all three must stay in lockstep.
/// </summary>
public static class SeatNumbering
{
    /// <summary>
    /// Computes the 1-based display number of every seat, keyed by Seat.Id. The input must be ALL seats
    /// of the room(s) involved — active and inactive — since a blocked seat still occupies a slot and
    /// leaving it out would wrongly shift its neighbours.
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> Compute(IEnumerable<Seat> roomSeats)
    {
        var result = new Dictionary<Guid, int>();

        foreach (var rowGroup in roomSeats.GroupBy(s => (s.RoomId, s.RowName)))
        {
            var ordered = rowGroup.OrderBy(s => s.ColIndex).ToList();
            var groupSizes = ordered
                .Where(s => s.SeatGroupId.HasValue)
                .GroupBy(s => s.SeatGroupId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var number = 0;
            Guid? previousGroupId = null;
            foreach (var seat in ordered)
            {
                var joinsPrevious = seat.SeatGroupId.HasValue
                    && previousGroupId == seat.SeatGroupId
                    && groupSizes[seat.SeatGroupId.Value] == 2;
                if (!joinsPrevious)
                {
                    number++;
                }
                result[seat.Id] = number;
                previousGroupId = seat.SeatGroupId;
            }
        }

        return result;
    }

    /// <summary>Builds the display label, e.g. "C2", from a row name and a display number.</summary>
    public static string Label(string rowName, int displayNumber)
    {
        return $"{rowName}{displayNumber}";
    }

    /// <summary>The label a customer-facing view should show for a sold ticket: the frozen SeatLabel when
    /// present, else a fallback to the seat's live physical RowName+ColIndex (rows written before this
    /// column existed, or not yet backfilled).</summary>
    public static string TicketLabel(InvoiceTicket ticket)
    {
        if (!string.IsNullOrEmpty(ticket.SeatLabel))
        {
            return ticket.SeatLabel;
        }
        if (ticket.Seat == null)
        {
            return string.Empty;
        }
        return $"{ticket.Seat.RowName}{ticket.Seat.ColIndex}";
    }
}
