using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class RoomManager(IApplicationUnitOfWork uow)
    : IRoomManager
{
    protected readonly IApplicationUnitOfWork _uow = uow;

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.RoomStore.ExistsAsync(e => e.Id == id);
    }

    public async Task<DefaultSearchResults<RoomDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var keyword = search.Filters.GetString("keyword");
        var theaterId = search.Filters.GetGuid("theaterId");
        var roomTypeId = search.Filters.GetGuid("roomTypeId");
        var status = search.Filters.GetEnum<RoomStatus>("status");

        Expression<Func<Room, bool>> predicate = e =>
            (string.IsNullOrEmpty(keyword) || e.Name.Contains(keyword!)) &&
            (theaterId == null || e.TheaterId == theaterId) &&
            (roomTypeId == null || e.RoomTypeId == roomTypeId) &&
            (status == null || e.Status == status);

        var total = await _uow.RoomStore.CountAsync(predicate);
        var items = await _uow.RoomStore.FindAllPageAsync(page - 1, pageSize, predicate);
        return PagingHelper.ToPagedResult<Room, RoomDTO>(items, total, page, pageSize);
    }

    public async Task<RoomDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.RoomStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Room {id} not found.");
        }
        return entity.ToDTO<Room, RoomDTO>();
    }

    public async Task<RoomDTO> CreateAsync(CreateRoomRequest request)
    {
        var entity = request.ToNewEntity<CreateRoomRequest, Room>();
        await _uow.RoomStore.CreateAsync(entity);
        await GenerateSeatsAsync(entity.Id, entity.TheaterId, entity.TotalRows, entity.TotalColumns);
        return entity.ToDTO<Room, RoomDTO>();
    }

    public async Task<RoomDTO> UpdateAsync(UpdateRoomRequest request)
    {
        var entity = await _uow.RoomStore.GetByIdAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Room {request.Id} not found.");
        }
        var dimensionsChanged = entity.TotalRows != request.TotalRows || entity.TotalColumns != request.TotalColumns;
        entity.PatchEntity<Room, UpdateRoomRequest>(request);
        await _uow.RoomStore.UpdateAsync(entity);

        // Rebuild the seat map only when the grid size actually changes.
        if (dimensionsChanged)
        {
            // Single bulk delete instead of one round trip per seat (avoids an N+1 query pattern).
            await _uow.SeatStore.DeleteAsync(s => s.RoomId == entity.Id);
            await GenerateSeatsAsync(entity.Id, entity.TheaterId, request.TotalRows, request.TotalColumns);
        }
        return entity.ToDTO<Room, RoomDTO>();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.RoomStore.DeleteAsync(id);
    }

    /// <summary>Creates a seat for every cell of the room's row × column grid.</summary>
    private async Task GenerateSeatsAsync(Guid roomId, Guid theaterId, int rows, int columns)
    {
        if (rows <= 0 || columns <= 0)
        {
            return;
        }

        var seatTypeId = (await _uow.SeatTypeStore.FindAsync(st => st.TheaterId == theaterId)).FirstOrDefault()?.Id ?? Guid.Empty;
        if (seatTypeId == Guid.Empty)
        {
            // No seat types for this theater yet — nothing valid to attach seats to.
            return;
        }

        var seats = new List<Seat>();
        for (var r = 0; r < rows; r++)
        {
            var rowName = ToRowName(r);
            for (var c = 1; c <= columns; c++)
            {
                seats.Add(new Seat
                {
                    RoomId     = roomId,
                    RowName    = rowName,
                    ColIndex   = c,
                    SeatTypeId = seatTypeId,
                    IsActive   = true,
                });
            }
        }
        await _uow.SeatStore.CreateRangeAsync(seats);
    }

    /// <summary>0 → "A", 25 → "Z", 26 → "AA", …</summary>
    private static string ToRowName(int index)
    {
        var name = string.Empty;
        index++;
        while (index > 0)
        {
            index--;
            name = (char)('A' + index % 26) + name;
            index /= 26;
        }
        return name;
    }

    public async Task<List<RoomSeatDTO>> GetSeatMapAsync(Guid roomId)
    {
        var seats = await _uow.SeatStore.FindAsync(s => s.RoomId == roomId);
        var types = (await _uow.SeatTypeStore.GetAllAsync()).ToDictionary(t => t.Id);
        return seats
            .OrderBy(s => s.RowName).ThenBy(s => s.ColIndex)
            .Select(s =>
            {
                types.TryGetValue(s.SeatTypeId, out var t);
                return new RoomSeatDTO
                {
                    Id            = s.Id,
                    RowName       = s.RowName,
                    ColIndex      = s.ColIndex,
                    SeatTypeId    = s.SeatTypeId,
                    SeatTypeName  = t?.Name ?? string.Empty,
                    SeatTypeColor = t?.Color ?? "#808080",
                    PriceMultiplier = t?.PriceMultiplier ?? 1,
                    SeatGroupId   = s.SeatGroupId,
                    IsActive      = s.IsActive,
                };
            })
            .ToList();
    }

    public async Task SaveSeatMapAsync(SaveSeatMapRequest request)
    {
        // FindAsync returns tracked entities on the shared context, so mutating them
        // and saving once persists every assignment in a single round-trip.
        var seats = (await _uow.SeatStore.FindAsync(s => s.RoomId == request.RoomId))
            .ToDictionary(s => s.Id);
        foreach (var item in request.Seats)
        {
            if (!seats.TryGetValue(item.SeatId, out var seat))
            {
                continue;
            }
            seat.SeatTypeId  = item.SeatTypeId;
            seat.SeatGroupId = item.SeatGroupId;
            seat.IsActive    = item.IsActive;
        }
        await _uow.SaveChangesAsync();
    }

    public async Task<List<RoomSeatDTO>> ResizeSeatGridAsync(ResizeSeatGridRequest request)
    {
        var room = await _uow.RoomStore.GetByIdAsync(request.RoomId);
        if (room == null)
        {
            throw new KeyNotFoundException($"Room {request.RoomId} not found.");
        }

        var rows = Math.Max(0, request.TotalRows);
        var columns = Math.Max(0, request.TotalColumns);

        // Cells the grid should contain after the resize.
        var desired = new HashSet<(string Row, int Col)>();
        for (var r = 0; r < rows; r++)
        {
            var rowName = ToRowName(r);
            for (var c = 1; c <= columns; c++)
            {
                desired.Add((rowName, c));
            }
        }

        var existing = (await _uow.SeatStore.FindAsync(s => s.RoomId == room.Id)).ToList();

        // Drop seats that fall outside the new grid (trimmed rows/columns) — a single bulk
        // delete by id instead of one round trip per seat (avoids an N+1 query pattern).
        var idsToDelete = existing
            .Where(s => !desired.Contains((s.RowName, s.ColIndex)))
            .Select(s => s.Id)
            .ToList();
        if (idsToDelete.Count > 0)
        {
            await _uow.SeatStore.DeleteAsync(s => idsToDelete.Contains(s.Id));
        }

        // Create seats for the appended cells; seats that stay keep their type + grouping.
        var present = existing.Select(s => (s.RowName, s.ColIndex)).ToHashSet();
        var seatTypeId = (await _uow.SeatTypeStore.FindAsync(st => st.TheaterId == room.TheaterId)).FirstOrDefault()?.Id ?? Guid.Empty;
        if (seatTypeId != Guid.Empty)
        {
            var toAdd = desired
                .Where(cell => !present.Contains(cell))
                .Select(cell => new Seat
                {
                    RoomId     = room.Id,
                    RowName    = cell.Row,
                    ColIndex   = cell.Col,
                    SeatTypeId = seatTypeId,
                    IsActive   = true,
                })
                .ToList();
            if (toAdd.Count > 0)
            {
                await _uow.SeatStore.CreateRangeAsync(toAdd);
            }
        }

        // Keep the room's stored dimensions in step with its seat grid.
        room.TotalRows = rows;
        room.TotalColumns = columns;
        await _uow.RoomStore.UpdateAsync(room);

        return await GetSeatMapAsync(room.Id);
    }
}
