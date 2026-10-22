using Cinema.Data.Entities;

namespace Cinema.Business.Helpers;

/// <summary>
/// Resolves a seat's real kind. Seat.Kind only ever stores Standard or Aisle — Double is still
/// derived from SeatGroupId — so code must read through EffectiveKind instead of inspecting
/// SeatGroupId.HasValue directly.
/// </summary>
public static class SeatKinds
{
    public static bool IsAisle(this Seat seat)
    {
        return seat.Kind == SeatKind.Aisle;
    }

    public static SeatKind EffectiveKind(this Seat seat)
    {
        if (seat.Kind == SeatKind.Aisle)
        {
            return SeatKind.Aisle;
        }
        if (seat.SeatGroupId.HasValue)
        {
            return SeatKind.Double;
        }
        return SeatKind.Standard;
    }
}
