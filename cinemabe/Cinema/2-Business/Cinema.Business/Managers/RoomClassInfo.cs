using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

/// <summary>Snapshot of the room's commercial class and availability this manager validates
/// against, loaded once per Create/Update instead of separate tracked Room/Theater fetches.
/// Public so the store's <c>FindSelectAsync&lt;Class&gt;</c> explicit type argument can be named
/// from the test project's mock setup.</summary>
public sealed record RoomClassInfo(string Name, bool SupportsThreeD, int TurnoverBufferMinutes, RoomStatus RoomStatus, string TheaterName, bool TheaterIsActive);
