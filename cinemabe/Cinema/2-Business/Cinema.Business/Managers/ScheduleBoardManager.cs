using Cinema.Business.Contracts;
using Cinema.Business.DTO.Operations;
using Cinema.Data.Contracts;

namespace Cinema.Business.Managers;

public class ScheduleBoardManager : IScheduleBoardManager
{
    private readonly IApplicationUnitOfWork _uow;

    public ScheduleBoardManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ScheduleBoardDTO> GetScheduleBoardAsync(Guid theaterId, DateTime date)
    {
        var from = date.Date;
        var to = from.AddDays(1);

        var rooms = await _uow.ScheduleBoardStore.GetRoomsAsync(theaterId);
        var showTimes = await _uow.ScheduleBoardStore.GetShowTimesAsync(theaterId, from, to);
        var sold = await _uow.ScheduleBoardStore.GetSoldCountsAsync(theaterId, from, to);

        var showTimesByRoom = showTimes.ToLookup(s => s.RoomId);
        var board = new ScheduleBoardDTO { TheaterId = theaterId, Date = from };
        foreach (var room in rooms)
        {
            var roomDto = new ScheduleBoardRoomDTO
            {
                RoomId = room.RoomId,
                RoomName = room.RoomName,
                RoomTypeName = room.RoomTypeName,
                RoomStatus = room.Status,
                TurnoverBufferMinutes = room.TurnoverBufferMinutes,
                Capacity = room.Capacity
            };
            foreach (var showTime in showTimesByRoom[room.RoomId].OrderBy(s => s.StartTime))
            {
                sold.TryGetValue((showTime.ShowTimeId, room.RoomId), out var soldCount);
                roomDto.ShowTimes.Add(new ScheduleBoardShowTimeDTO
                {
                    ShowTimeId = showTime.ShowTimeId,
                    MovieId = showTime.MovieId,
                    MovieTitle = showTime.MovieTitle,
                    Start = showTime.StartTime,
                    End = showTime.EndTime,
                    BufferEnd = showTime.EndTime.AddMinutes(room.TurnoverBufferMinutes),
                    Sold = soldCount,
                    Capacity = room.Capacity,
                    RoomStatus = room.Status
                });
            }
            board.Rooms.Add(roomDto);
        }
        return board;
    }
}
