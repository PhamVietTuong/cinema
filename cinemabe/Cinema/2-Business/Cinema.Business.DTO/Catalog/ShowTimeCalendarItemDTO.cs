using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

/// <summary>A showtime as drawn on the admin week calendar. Deliberately slimmer than <see cref="ShowTimeDTO"/>:
/// no room/room-type names or price — the full record is fetched with <c>GetShowTime</c> when a block is opened.</summary>
public class ShowTimeCalendarItemDTO
{
    public Guid Id { get; set; }
    public Guid MovieId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ProjectionForm ProjectionForm { get; set; }
    public ShowTimeType ShowTimeType { get; set; }
    public Guid? RoomId { get; set; }

    /// <summary>Minutes of turnover reserved after this showtime (0 when no room or no buffer configured).</summary>
    public int TurnoverBufferMinutes { get; set; }
}
