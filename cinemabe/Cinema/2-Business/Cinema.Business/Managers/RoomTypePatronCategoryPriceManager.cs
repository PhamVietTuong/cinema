using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class RoomTypePatronCategoryPriceManager : IRoomTypePatronCategoryPriceManager
{
    private readonly IApplicationUnitOfWork _uow;

    public RoomTypePatronCategoryPriceManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<RoomTypePatronCategoryPriceDTO>> GetByRoomTypeAsync(Guid roomTypeId)
    {
        var roomType = await _uow.RoomTypeStore.GetByIdAsync(roomTypeId);
        if (roomType == null)
        {
            throw new KeyNotFoundException($"RoomType {roomTypeId} not found.");
        }

        var categories = (await _uow.PatronCategoryStore.FindAsync(c => c.TheaterId == roomType.TheaterId && c.IsActive)).ToList();
        var seatTypeIds = categories.Select(c => c.SeatTypeId).Distinct().ToList();
        var seatTypes = seatTypeIds.Count == 0
            ? new Dictionary<Guid, SeatType>()
            : (await _uow.SeatTypeStore.FindAsync(s => seatTypeIds.Contains(s.Id))).ToDictionary(s => s.Id);

        var categoryIds = categories.Select(c => c.Id).ToList();
        var overrides = categoryIds.Count == 0
            ? new Dictionary<Guid, RoomTypePatronCategoryPrice>()
            : (await _uow.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(categoryIds))
                .Where(o => o.RoomTypeId == roomTypeId)
                .ToDictionary(o => o.PatronCategoryId);

        return categories.Select(c =>
        {
            seatTypes.TryGetValue(c.SeatTypeId, out var seatType);
            var hasOverride = overrides.TryGetValue(c.Id, out var over);
            return new RoomTypePatronCategoryPriceDTO
            {
                Id               = hasOverride ? over!.Id : Guid.Empty,
                RoomTypeId       = roomTypeId,
                PatronCategoryId = c.Id,
                PatronCategoryName = c.Name,
                SeatTypeId       = c.SeatTypeId,
                SeatTypeName     = seatType?.Name ?? string.Empty,
                Kind             = seatType?.Kind ?? default,
                DefaultPrice     = c.Price,
                IsIncluded       = hasOverride,
                Price            = hasOverride ? over!.Price : c.Price,
            };
        }).ToList();
    }

    public async Task SaveAsync(SaveRoomTypePatronCategoryPricesRequest request)
    {
        var roomType = await _uow.RoomTypeStore.GetByIdAsync(request.RoomTypeId);
        if (roomType == null)
        {
            throw new KeyNotFoundException($"RoomType {request.RoomTypeId} not found.");
        }

        var categoryIds = request.Items.Select(i => i.PatronCategoryId).Distinct().ToList();
        var categoriesById = (await _uow.PatronCategoryStore.FindAsync(c => categoryIds.Contains(c.Id) && c.TheaterId == roomType.TheaterId))
            .ToDictionary(c => c.Id, c => c.Price);
        if (categoriesById.Count != categoryIds.Count)
        {
            throw new InvalidOperationException("One or more patron categories do not belong to this room type's theater.");
        }
        if (request.Items.Any(i => i.Included && i.Price is < 0))
        {
            throw new InvalidOperationException("Price must not be negative.");
        }

        var existing = (await _uow.RoomTypePatronCategoryPriceStore.FindByRoomTypeAsync(request.RoomTypeId))
            .ToDictionary(o => o.PatronCategoryId);

        await _uow.BeginTransactionAsync();
        try
        {
            foreach (var item in request.Items)
            {
                var hasExisting = existing.TryGetValue(item.PatronCategoryId, out var current);
                if (!item.Included)
                {
                    if (hasExisting)
                    {
                        await _uow.RoomTypePatronCategoryPriceStore.DeleteAsync(current!);
                    }
                    continue;
                }

                var price = item.Price ?? categoriesById[item.PatronCategoryId];
                if (hasExisting)
                {
                    current!.Price = price;
                    await _uow.RoomTypePatronCategoryPriceStore.UpdateAsync(current);
                }
                else
                {
                    await _uow.RoomTypePatronCategoryPriceStore.CreateAsync(new RoomTypePatronCategoryPrice
                    {
                        RoomTypeId       = request.RoomTypeId,
                        PatronCategoryId = item.PatronCategoryId,
                        Price            = price,
                    });
                }
            }
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }
}
