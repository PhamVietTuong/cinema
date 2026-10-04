using Cinema.Business.DTO.Operations;

namespace Cinema.Business.Contracts;

public interface IScheduleBoardManager
{
    /// <summary>
    /// Rooms of the theater, each with that day's showtimes as start/end/bufferEnd/movie/sold/capacity/roomStatus.
    /// Three queries in total (rooms, showtimes, one grouped sold count), however many showtimes there are.
    /// </summary>
    Task<ScheduleBoardDTO> GetScheduleBoardAsync(Guid theaterId, DateTime date);
}
