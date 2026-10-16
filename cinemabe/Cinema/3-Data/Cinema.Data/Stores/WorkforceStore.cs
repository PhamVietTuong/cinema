using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class WorkforceStore : IWorkforceStore
{
    private readonly CinemaContext _db;

    public WorkforceStore(CinemaContext db)
    {
        _db = db;
    }

    // ── Roster ───────────────────────────────────────────────────────────────

    public async Task<List<StaffShift>> GetShiftsAsync(Guid theaterId, DateTime from, DateTime to, Guid? userId)
    {
        var query = _db.StaffShift.AsNoTracking()
            .Where(s => s.TheaterId == theaterId && s.StartTime < to && s.EndTime > from);
        if (userId.HasValue)
        {
            var id = userId.Value;
            query = query.Where(s => s.UserId == id);
        }
        return await query.OrderBy(s => s.StartTime).ThenBy(s => s.UserId).ToListAsync();
    }

    public async Task<List<StaffShift>> GetUserShiftsAsync(Guid userId, DateTime from, DateTime to)
    {
        return await _db.StaffShift.AsNoTracking()
            .Where(s => s.UserId == userId && s.StartTime < to && s.EndTime > from)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<StaffShift?> GetShiftAsync(Guid id)
    {
        return await _db.StaffShift.FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<bool> HasShiftOverlapAsync(Guid userId, DateTime start, DateTime end, Guid? excludeShiftId)
    {
        return await _db.StaffShift.AnyAsync(s => s.UserId == userId
            && s.StartTime < end
            && s.EndTime > start
            && (excludeShiftId == null || s.Id != excludeShiftId));
    }

    public void StageShift(StaffShift shift)
    {
        _db.StaffShift.Add(shift);
    }

    public void StageDeleteShift(StaffShift shift)
    {
        _db.StaffShift.Remove(shift);
    }

    public async Task<List<TheaterStaffRow>> GetTheaterStaffAsync(Guid theaterId, IReadOnlyCollection<string> roleNames)
    {
        return await _db.User.AsNoTracking()
            .Where(u => u.TheaterId == theaterId && u.Status == UserStatus.Active && roleNames.Contains(u.UserType.Name))
            .OrderBy(u => u.Name)
            .Select(u => new TheaterStaffRow(u.Id, u.Name, u.UserType.Name))
            .ToListAsync();
    }

}
