using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class RoomManager : IRoomManager
{
    protected readonly IApplicationUnitOfWork _uow;

    public RoomManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.RoomStore.ExistsAsync(e => e.Id == id);
    }

    private IQueryable<Room> GetFilteredRoomQuery(Dictionary<string, string>? filters)
    {
        var query = _uow.RoomStore.GetQuery();
        if (filters == null)
        {
            return query;
        }

        foreach (var key in filters.Keys)
        {
            if (string.IsNullOrEmpty(filters[key]))
            {
                continue;
            }

            switch (key)
            {
                case "keyword":
                    var keyword = filters[key];
                    query = _uow.RoomStore.FilterQuery(query, e => e.Name.Contains(keyword));
                    break;

                case "theaterId":
                    if (Guid.TryParse(filters[key], out var theaterId))
                    {
                        query = _uow.RoomStore.FilterQuery(query, e => e.TheaterId == theaterId);
                    }
                    break;

                case "roomTypeId":
                    if (Guid.TryParse(filters[key], out var roomTypeId))
                    {
                        query = _uow.RoomStore.FilterQuery(query, e => e.RoomTypeId == roomTypeId);
                    }
                    break;

                case "status":
                    if (Enum.TryParse<RoomStatus>(filters[key], true, out var status))
                    {
                        query = _uow.RoomStore.FilterQuery(query, e => e.Status == status);
                    }
                    break;
            }
        }
        return query;
    }

    private IQueryable<Room> ApplySort(IQueryable<Room> query, SortDTO? sort)
    {
        if (sort == null || string.IsNullOrEmpty(sort.Field))
        {
            return query;
        }

        return sort.Field switch
        {
            "name" => _uow.RoomStore.OrderQuery(query, e => e.Name, sort.Ascending),
            "status" => _uow.RoomStore.OrderQuery(query, e => e.Status, sort.Ascending),
            _ => query,
        };
    }

    public async Task<DefaultSearchResults<RoomDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);

        var query = GetFilteredRoomQuery(search.Filters);
        query = ApplySort(query, search.Sort);
        var total = await _uow.RoomStore.CountAsync(query);
        var items = await _uow.RoomStore.AllPageAsync(query, page - 1, pageSize);
        var result = PagingHelper.ToPagedResult<Room, RoomDTO>(items, total, page, pageSize);

        // One grouped query for the whole page of rooms, not one count per room.
        var seatCounts = await _uow.SeatStore.GetBookableSeatCountsAsync(items.Select(r => r.Id).ToList());
        foreach (var dto in result.Results)
        {
            seatCounts.TryGetValue(dto.Id, out var count);
            dto.SeatCount = count;
        }
        return result;
    }

    public async Task<RoomDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.RoomStore.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Room {id} not found.");
        }
        var dto = entity.ToDTO<Room, RoomDTO>();
        var seatCounts = await _uow.SeatStore.GetBookableSeatCountsAsync([id]);
        seatCounts.TryGetValue(id, out var count);
        dto.SeatCount = count;
        return dto;
    }

    public async Task<RoomDTO> CreateAsync(CreateRoomRequest request)
    {
        var entity = request.ToNewEntity<CreateRoomRequest, Room>();
        await _uow.RoomStore.CreateAsync(entity);
        await GenerateSeatsAsync(entity.Id, entity.TotalRows, entity.TotalColumns);
        // A freshly generated grid has no aisles, so every cell is bookable.
        var dto = entity.ToDTO<Room, RoomDTO>();
        dto.SeatCount = entity.TotalRows * entity.TotalColumns;
        return dto;
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

        var dto = entity.ToDTO<Room, RoomDTO>();
        if (dimensionsChanged)
        {
            // Rebuild the seat map only when the grid size actually changes.
            // Single bulk delete instead of one round trip per seat (avoids an N+1 query pattern).
            await _uow.SeatStore.DeleteAsync(s => s.RoomId == entity.Id);
            await GenerateSeatsAsync(entity.Id, request.TotalRows, request.TotalColumns);
            // The rebuilt grid has no aisles, so every cell is bookable.
            dto.SeatCount = request.TotalRows * request.TotalColumns;
        }
        else
        {
            var seatCounts = await _uow.SeatStore.GetBookableSeatCountsAsync([entity.Id]);
            seatCounts.TryGetValue(entity.Id, out var count);
            dto.SeatCount = count;
        }
        return dto;
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.RoomStore.DeleteAsync(id);
    }

    /// <summary>Creates a seat for every cell of the room's row × column grid. A seat has no stored
    /// kind of its own (Standard/Double is derived from SeatGroupId at read time), so nothing in a
    /// theater's configuration can block this — every seat starts Standard/ungrouped.</summary>
    private async Task GenerateSeatsAsync(Guid roomId, int rows, int columns)
    {
        if (rows <= 0 || columns <= 0)
        {
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
                    RoomId   = roomId,
                    RowName  = rowName,
                    ColIndex = c,
                    IsActive = true,
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
        var seats = await _uow.SeatStore.GetSeatMapByRoomNoTrackingAsync(roomId);

        // A SeatGroupId only makes a valid double seat when exactly 2 seats share it (mirrors the
        // validation SaveSeatMapAsync already applies on write). Stale/malformed data — a seat whose
        // group has 1 or 3+ members, left over from before that validation existed, or from a removal
        // that couldn't find its partner — must read back as a plain Standard seat, not a broken double.
        var groupSizes = seats
            .Where(s => !s.IsAisle() && s.SeatGroupId.HasValue)
            .GroupBy(s => s.SeatGroupId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return seats
            .Select(s =>
            {
                var isValidPair = !s.IsAisle() && s.SeatGroupId.HasValue && groupSizes[s.SeatGroupId.Value] == 2;
                return new RoomSeatDTO
                {
                    Id          = s.Id,
                    RowName     = s.RowName,
                    ColIndex    = s.ColIndex,
                    IsDouble    = isValidPair,
                    IsAisle     = s.IsAisle(),
                    SeatGroupId = isValidPair ? s.SeatGroupId : null,
                    IsActive    = s.IsActive,
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

        // A SeatGroupId links exactly two seats into a double seat. Anything else
        // (an orphan or a group of 3+) is invalid data and must not be persisted.
        // Aisle cells never pair, so they're excluded from the group-size tally.
        var groupSizes = request.Seats
            .Where(item => !item.IsAisle && item.SeatGroupId.HasValue)
            .GroupBy(item => item.SeatGroupId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var turningIntoAisle = request.Seats
            .Where(item => item.IsAisle)
            .Select(item => item.SeatId)
            .Where(id => seats.TryGetValue(id, out var seat) && !seat.IsAisle())
            .ToList();
        if (turningIntoAisle.Count > 0)
        {
            var affected = await _uow.IncidentStore.GetUpcomingTicketsAsync(request.RoomId, turningIntoAisle, DateTime.Now);
            if (affected.Count > 0)
            {
                throw new InvalidOperationException("Cannot turn a seat with upcoming tickets into an aisle.");
            }
        }

        foreach (var item in request.Seats)
        {
            if (!seats.TryGetValue(item.SeatId, out var seat))
            {
                continue;
            }
            if (item.IsAisle)
            {
                seat.Kind        = SeatKind.Aisle;
                seat.SeatGroupId = null;
                seat.IsActive    = true;
                continue;
            }
            seat.Kind        = SeatKind.Standard;
            seat.SeatGroupId = item.SeatGroupId.HasValue && groupSizes[item.SeatGroupId.Value] == 2
                ? item.SeatGroupId
                : null;
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
        var keptRowNames = Enumerable.Range(0, rows).Select(ToRowName).ToHashSet();

        var existing = (await _uow.SeatStore.FindAsync(s => s.RoomId == room.Id)).ToList();
        var existingByRow = existing.GroupBy(s => s.RowName).ToDictionary(g => g.Key, g => g.ToList());

        // Drop whole rows trimmed off the grid — a single bulk delete by id instead of one round trip
        // per seat (avoids an N+1 query pattern).
        var idsToDelete = existing
            .Where(s => !keptRowNames.Contains(s.RowName))
            .Select(s => s.Id)
            .ToList();

        var toAdd = new List<Seat>();
        // Per row, not per rectangle: a row may already have fewer seats than `columns` (a prior
        // single-seat removal shortened it), and the grid must not resurrect that removed seat.
        foreach (var rowName in keptRowNames)
        {
            if (!existingByRow.TryGetValue(rowName, out var rowSeats) || rowSeats.Count == 0)
            {
                // A newly-appended row: fill it in full.
                for (var c = 1; c <= columns; c++)
                {
                    toAdd.Add(new Seat { RoomId = room.Id, RowName = rowName, ColIndex = c, IsActive = true });
                }
                continue;
            }

            var len = rowSeats.Count;
            if (columns > len)
            {
                for (var c = len + 1; c <= columns; c++)
                {
                    toAdd.Add(new Seat { RoomId = room.Id, RowName = rowName, ColIndex = c, IsActive = true });
                }
            }
            else if (columns < len)
            {
                idsToDelete.AddRange(rowSeats.Where(s => s.ColIndex > columns).Select(s => s.Id));
            }
        }

        if (idsToDelete.Count > 0)
        {
            await _uow.SeatStore.DeleteAsync(s => idsToDelete.Contains(s.Id));
        }
        if (toAdd.Count > 0)
        {
            await _uow.SeatStore.CreateRangeAsync(toAdd);
        }

        // Keep the room's stored dimensions in step with its seat grid.
        room.TotalRows = rows;
        room.TotalColumns = columns;
        await _uow.RoomStore.UpdateAsync(room);

        return await GetSeatMapAsync(room.Id);
    }

    public async Task<List<RoomSeatDTO>> RemoveSeatAsync(RemoveSeatRequest request)
    {
        var room = await _uow.RoomStore.GetByIdAsync(request.RoomId);
        if (room == null)
        {
            throw new KeyNotFoundException($"Room {request.RoomId} not found.");
        }

        var seat = (await _uow.SeatStore.FindAsync(s => s.Id == request.SeatId && s.RoomId == request.RoomId)).SingleOrDefault();
        if (seat == null)
        {
            throw new KeyNotFoundException($"Seat {request.SeatId} not found.");
        }

        var hasUpcomingShowTime = await _uow.ShowTimeStore.ExistsAsync(
            s => s.IsActive && s.EndTime > DateTime.Now && s.ShowTimeRooms.Any(r => r.RoomId == request.RoomId));
        if (hasUpcomingShowTime)
        {
            throw new InvalidOperationException("Cannot remove a seat while the room has upcoming showtimes.");
        }

        if (await _uow.SeatStore.HasAnyTicketAsync(seat.Id))
        {
            throw new InvalidOperationException("This seat has ticket history and cannot be removed.");
        }

        var seatCountInRow = await _uow.SeatStore.CountAsync(s => s.RoomId == request.RoomId && s.RowName == seat.RowName);
        if (seatCountInRow <= 1)
        {
            throw new InvalidOperationException("Cannot remove the last seat of a row.");
        }

        await _uow.BeginTransactionAsync();
        try
        {
            if (seat.SeatGroupId.HasValue)
            {
                var partners = await _uow.SeatStore.FindAsync(
                    s => s.RoomId == request.RoomId && s.SeatGroupId == seat.SeatGroupId && s.Id != seat.Id);
                foreach (var partner in partners)
                {
                    partner.SeatGroupId = null;
                }
            }

            // Saves the partner's change too, since both are tracked on the same context.
            await _uow.SeatStore.DeleteAsync(seat);
            await _uow.SeatStore.ShiftRowLeftAsync(request.RoomId, seat.RowName, seat.ColIndex);

            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        return await GetSeatMapAsync(request.RoomId);
    }
}
