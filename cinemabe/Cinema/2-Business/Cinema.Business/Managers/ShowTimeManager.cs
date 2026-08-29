using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class ShowTimeManager(IApplicationUnitOfWork uow)
    : IShowTimeManager
{
    protected readonly IApplicationUnitOfWork _uow = uow;

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _uow.ShowTimeStore.ExistsAsync(e => e.Id == id);
    }

    public async Task<DefaultSearchResults<ShowTimeDTO>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var page = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize > 0 ? search.PageSize : 20;

        var (items, total) = await _uow.ShowTimeStore.SearchAsync(
            search.Filters.GetGuid("movieId"),
            search.Filters.GetGuid("roomId"),
            search.Filters.GetBool("isActive"),
            search.Filters.GetDateTime("from"),
            search.Filters.GetDateTime("to"),
            page, pageSize);

        return new DefaultSearchResults<ShowTimeDTO>
        {
            Results = items.Select(ToShowTimeDTO).ToList(),
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page
        };
    }

    public async Task<ShowTimeDTO> GetByIdAsync(Guid id)
    {
        var entity = await _uow.ShowTimeStore.GetByIdWithRoomsAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"ShowTime {id} not found.");
        }
        return ToShowTimeDTO(entity);
    }

    public async Task<ShowTimeDTO> CreateAsync(CreateShowTimeRequest request)
    {
        ValidateWindow(request.StartTime, request.EndTime, mustBeFuture: true);

        var entity = request.ToNewEntity<CreateShowTimeRequest, ShowTime>();
        if (request.RoomId != Guid.Empty)
        {
            if (await _uow.ShowTimeStore.HasRoomOverlapAsync(request.RoomId, entity.StartTime, entity.EndTime, null))
            {
                throw new InvalidOperationException("This room already has a showtime overlapping that time window.");
            }
            entity.ShowTimeRooms.Add(new ShowTimeRoom { RoomId = request.RoomId, BasePrice = request.BasePrice });
        }
        // Single SaveChanges inserts the showtime and its room together (atomic).
        await _uow.ShowTimeStore.CreateAsync(entity);
        return ToShowTimeDTO(await _uow.ShowTimeStore.GetByIdWithRoomsAsync(entity.Id) ?? entity);
    }

    public async Task<ShowTimeDTO> UpdateAsync(UpdateShowTimeRequest request)
    {
        // Editing an existing showtime doesn't require a future start (an admin may be correcting
        // the record of one that already screened), but the window must still make sense.
        ValidateWindow(request.StartTime, request.EndTime, mustBeFuture: false);

        var entity = await _uow.ShowTimeStore.GetByIdWithRoomsAsync(request.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"ShowTime {request.Id} not found.");
        }
        entity.PatchEntity<ShowTime, UpdateShowTimeRequest>(request);
        entity.LastUpdatedTime = DateTime.UtcNow;
        if (request.RoomId != Guid.Empty &&
            await _uow.ShowTimeStore.HasRoomOverlapAsync(request.RoomId, entity.StartTime, entity.EndTime, entity.Id))
        {
            throw new InvalidOperationException("This room already has a showtime overlapping that time window.");
        }
        ApplyRoom(entity, request.RoomId, request.BasePrice);
        // The showtime patch and room change are saved in one transaction on the tracked graph.
        await _uow.SaveChangesAsync();
        return ToShowTimeDTO(await _uow.ShowTimeStore.GetByIdWithRoomsAsync(request.Id) ?? entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _uow.ShowTimeStore.DeleteAsync(id);
    }

    /// <summary>
    /// Rejects nonsensical showtime windows. Without this a showtime could be saved ending before
    /// it starts — which also slipped past the room-overlap check, since an inverted window
    /// overlaps nothing — or scheduled into the past where nobody can book it.
    /// </summary>
    private static void ValidateWindow(DateTime startTime, DateTime endTime, bool mustBeFuture)
    {
        if (endTime <= startTime)
        {
            throw new InvalidOperationException("A showtime must end after it starts.");
        }
        if (mustBeFuture && startTime <= DateTime.Now)
        {
            throw new InvalidOperationException("A showtime cannot be scheduled in the past.");
        }
    }

    /// <summary>
    /// Reconciles a showtime's single room assignment. No-ops when nothing changed so that
    /// editing an already-booked showtime (whose room is referenced by invoices) doesn't
    /// attempt a restricted delete.
    /// </summary>
    private static void ApplyRoom(ShowTime entity, Guid roomId, int basePrice)
    {
        if (roomId == Guid.Empty) { return; }

        var current = entity.ShowTimeRooms.FirstOrDefault();
        if (current != null && current.RoomId == roomId && current.BasePrice == basePrice) { return; }

        entity.ShowTimeRooms.Clear();
        entity.ShowTimeRooms.Add(new ShowTimeRoom { ShowTimeId = entity.Id, RoomId = roomId, BasePrice = basePrice });
    }

    private static ShowTimeDTO ToShowTimeDTO(ShowTime s)
    {
        var dto = s.ToDTO<ShowTime, ShowTimeDTO>();
        var sr = s.ShowTimeRooms?.FirstOrDefault();
        if (sr != null)
        {
            dto.RoomId = sr.RoomId;
            dto.RoomName = sr.Room?.Name;
            dto.BasePrice = sr.BasePrice;
        }
        return dto;
    }
}
