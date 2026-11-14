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
    private readonly ISeatTypeManager _seatTypes;

    public TheaterManager(IApplicationUnitOfWork uow, ISeatTypeManager seatTypes)
    {
        _uow = uow;
        _seatTypes = seatTypes;
    }

    public async Task<DefaultSearchResults<TheaterDTO>> GetTheatersAsync(PagingSearchDTO search)
    {
        var name = search.Filters.GetString("name");
        var city = search.Filters.GetString("city");
        var page = search.PageIndex > 0 ? search.PageIndex : 1;

        var (items, total) = await _uow.TheaterStore.SearchAsync(name, city, page, search.PageSize);

        int countPerPage;
        if (search.PageSize > 0)
        {
            countPerPage = search.PageSize;
        }
        else
        {
            countPerPage = total;
        }

        return new DefaultSearchResults<TheaterDTO>
        {
            Results      = items.Select(r => ToTheaterDTO(r.Theater, r.RoomCount)).ToList(),
            TotalCount   = total,
            CountPerPage = countPerPage,
            Page         = page
        };
    }

    public async Task<DefaultSearchResults<TheaterDTO>> GetTheatersByMovieAsync(PagingSearchDTO search)
    {
        var movieId = search.Filters.GetGuid("movieId") ?? Guid.Empty;
        var date    = search.Filters.GetDateTime("date") ?? DateTime.Today;

        var theaters = (await _uow.TheaterStore.GetByMovieAsync(movieId, date)).Select(t => ToTheaterDTO(t)).ToList();
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
        ValidateMaxSeats(request.MaxSeatsPerBooking);
        ValidatePromotionCaps(request.MaxPromotionDiscountPercent, request.MaxHolidayPromotionDiscountPercent);
        var theater = request.ToNewEntity<CreateTheaterRequest, Theater>();
        await _uow.TheaterStore.CreateAsync(theater);
        await _seatTypes.EnsureDefaultsAsync(theater.Id);
        return ToTheaterDTO(theater);
    }

    public async Task<TheaterDTO> UpdateAsync(UpdateTheaterRequest request)
    {
        var theater = await _uow.TheaterStore.GetByIdAsync(request.Id);
        if (theater == null)
        {
            throw new KeyNotFoundException($"Theater {request.Id} not found.");
        }
        ValidateMaxSeats(request.MaxSeatsPerBooking);
        ValidatePromotionCaps(request.MaxPromotionDiscountPercent, request.MaxHolidayPromotionDiscountPercent);
        theater.PatchEntity<Theater, UpdateTheaterRequest>(request);
        // PatchEntity skips nullable-to-int properties, so the optional caps are applied here; null keeps the stored value.
        if (request.MaxPromotionDiscountPercent.HasValue)
        {
            theater.MaxPromotionDiscountPercent = request.MaxPromotionDiscountPercent.Value;
        }
        if (request.MaxHolidayPromotionDiscountPercent.HasValue)
        {
            theater.MaxHolidayPromotionDiscountPercent = request.MaxHolidayPromotionDiscountPercent.Value;
        }
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

    internal static void ValidatePromotionCaps(int? maxPercent, int? maxHolidayPercent)
    {
        if (maxPercent < 0 || maxPercent > 50)
        {
            throw new InvalidOperationException("Max promotion discount must be between 0 and 50 percent.");
        }
        if (maxHolidayPercent < 0 || maxHolidayPercent > 100)
        {
            throw new InvalidOperationException("Max holiday promotion discount must be between 0 and 100 percent.");
        }
    }

    private static void ValidateMaxSeats(int maxSeatsPerBooking)
    {
        if (maxSeatsPerBooking < 1 || maxSeatsPerBooking > 50)
        {
            throw new InvalidOperationException("Max seats per booking must be between 1 and 50.");
        }
    }

    private static TheaterDTO ToTheaterDTO(Theater theater)
    {
        var roomCount = 0;
        if (theater.Rooms != null)
        {
            roomCount = theater.Rooms.Count;
        }
        return ToTheaterDTO(theater, roomCount);
    }

    private static TheaterDTO ToTheaterDTO(Theater theater, int roomCount)
    {
        var dto = theater.ToDTO<Theater, TheaterDTO>();
        dto.RoomCount = roomCount;
        return dto;
    }
}
