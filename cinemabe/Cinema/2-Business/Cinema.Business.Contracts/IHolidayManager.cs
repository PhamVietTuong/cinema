using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IHolidayManager
{
    Task<DefaultSearchResults<HolidayDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                             ExistsAsync(Guid id);
    Task<HolidayDTO>                       GetByIdAsync(Guid id);
    Task<HolidayDTO>                       CreateAsync(CreateHolidayRequest request);
    Task<HolidayDTO>                       UpdateAsync(UpdateHolidayRequest request);
    Task                                   DeleteAsync(Guid id);

    /// <summary>Per-slot holiday overrides, filterable by <c>holidayId</c> and <c>theaterId</c> (the
    /// holiday's default factor applies to any slot without a row).</summary>
    Task<DefaultSearchResults<HolidayTimeSlotPriceDTO>> GetTimeSlotPricesAsync(PagingSearchDTO search);
    Task<List<HolidayTimeSlotPriceDTO>>    UpsertTimeSlotPricesAsync(UpsertHolidayTimeSlotPricesRequest request);
}
