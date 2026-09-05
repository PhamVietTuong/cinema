using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class PatronCategorySeatTypeStore : IPatronCategorySeatTypeStore
{
    private readonly CinemaContext _db;

    public PatronCategorySeatTypeStore(CinemaContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<PatronCategorySeatType>> FindByPatronCategoriesAsync(IReadOnlyCollection<Guid> patronCategoryIds)
    {
        return await _db.Set<PatronCategorySeatType>()
            .AsNoTracking()
            .Where(x => patronCategoryIds.Contains(x.PatronCategoryId))
            .ToListAsync();
    }

    public async Task ReplaceForPatronCategoryAsync(Guid patronCategoryId, IReadOnlyCollection<Guid> seatTypeIds)
    {
        var existing = await _db.Set<PatronCategorySeatType>()
            .Where(x => x.PatronCategoryId == patronCategoryId)
            .ToListAsync();
        _db.Set<PatronCategorySeatType>().RemoveRange(existing);

        foreach (var seatTypeId in seatTypeIds.Distinct())
        {
            _db.Set<PatronCategorySeatType>().Add(new PatronCategorySeatType
            {
                PatronCategoryId = patronCategoryId,
                SeatTypeId = seatTypeId
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteBySeatTypeAsync(Guid seatTypeId)
    {
        var existing = await _db.Set<PatronCategorySeatType>()
            .Where(x => x.SeatTypeId == seatTypeId)
            .ToListAsync();
        if (existing.Count > 0)
        {
            _db.Set<PatronCategorySeatType>().RemoveRange(existing);
            await _db.SaveChangesAsync();
        }
    }
}
