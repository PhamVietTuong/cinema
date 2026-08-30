using Cinema.Business.Contracts;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Theaters;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class TheaterManager : ITheaterManager
{
    private readonly IApplicationUnitOfWork _uow;

    public TheaterManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<DefaultSearchResults<TheaterDTO>> GetTheatersAsync(PagingSearchDTO search)
    {
        var name = search.Filters.GetString("name");
        var city = search.Filters.GetString("city");

        var theaters = (await _uow.TheaterStore.GetTheatersWithRoomsAsync()).Select(ToTheaterDTO).ToList();

        if (!string.IsNullOrWhiteSpace(name))
        {
            theaters = theaters.Where(t => t.Name != null && t.Name.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            theaters = theaters.Where(t => t.City != null && t.City.Contains(city, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var page     = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize  > 0 ? search.PageSize  : theaters.Count;
        var paged    = theaters.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new DefaultSearchResults<TheaterDTO>
        {
            Results      = paged,
            TotalCount   = theaters.Count,
            CountPerPage = pageSize,
            Page         = page
        };
    }

    public async Task<DefaultSearchResults<TheaterDTO>> GetTheatersByMovieAsync(PagingSearchDTO search)
    {
        var movieId = search.Filters.GetGuid("movieId") ?? Guid.Empty;
        var date    = search.Filters.GetDateTime("date") ?? DateTime.Today;

        var theaters = (await _uow.TheaterStore.GetByMovieAsync(movieId, date)).Select(ToTheaterDTO).ToList();
        return new DefaultSearchResults<TheaterDTO>
        {
            Results      = theaters,
            TotalCount   = theaters.Count,
            CountPerPage = theaters.Count,
            Page         = 1
        };
    }

    public async Task<TheaterDTO> GetByIdAsync(Guid id)
    {
        var theater = await _uow.TheaterStore.GetDetailAsync(id);
        if (theater == null)
        {
            throw new KeyNotFoundException($"Theater {id} not found.");
        }
        return ToTheaterDTO(theater);
    }

    public async Task<TheaterDTO> CreateAsync(CreateTheaterRequest request)
    {
        var theater = request.ToNewEntity<CreateTheaterRequest, Theater>();
        await _uow.TheaterStore.CreateAsync(theater);
        return ToTheaterDTO(theater);
    }

    public async Task<TheaterDTO> UpdateAsync(UpdateTheaterRequest request)
    {
        var theater = await _uow.TheaterStore.GetByIdAsync(request.Id);
        if (theater == null)
        {
            throw new KeyNotFoundException($"Theater {request.Id} not found.");
        }
        theater.PatchEntity<Theater, UpdateTheaterRequest>(request);
        await _uow.TheaterStore.UpdateAsync(theater);
        await _uow.SaveChangesAsync();
        return ToTheaterDTO(theater);
    }

    public async Task DeleteAsync(Guid id)
    {
        var theater = await _uow.TheaterStore.GetByIdAsync(id);
        if (theater == null)
        {
            throw new KeyNotFoundException($"Theater {id} not found.");
        }
        theater.IsActive = false;
        await _uow.TheaterStore.UpdateAsync(theater);
        await _uow.SaveChangesAsync();
    }

    private static TheaterDTO ToTheaterDTO(Theater theater)
    {
        var dto = theater.ToDTO<Theater, TheaterDTO>();
        dto.RoomCount = theater.Rooms?.Count ?? 0;
        return dto;
    }
}
