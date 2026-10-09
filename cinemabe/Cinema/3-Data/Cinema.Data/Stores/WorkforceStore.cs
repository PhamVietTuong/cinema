using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class WorkforceStore : IWorkforceStore
{
    private const int _maxUserTasks = 200;

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

    // ── Time clock ───────────────────────────────────────────────────────────

    public async Task<TimeClockEntry?> GetOpenClockEntryAsync(Guid userId)
    {
        return await _db.TimeClockEntry.FirstOrDefaultAsync(e => e.UserId == userId && e.ClockOutAt == null);
    }

    public async Task AddClockEntryAsync(TimeClockEntry entry)
    {
        _db.TimeClockEntry.Add(entry);
        await _db.SaveChangesAsync();
    }

    public async Task<List<TimeClockEntry>> GetClockEntriesAsync(Guid theaterId, DateTime fromUtc, DateTime toUtc, Guid? userId)
    {
        var query = _db.TimeClockEntry.AsNoTracking()
            .Where(e => e.TheaterId == theaterId && e.ClockInAt >= fromUtc && e.ClockInAt < toUtc);
        if (userId.HasValue)
        {
            var id = userId.Value;
            query = query.Where(e => e.UserId == id);
        }
        return await query.OrderByDescending(e => e.ClockInAt).ToListAsync();
    }

    // ── Tasks ────────────────────────────────────────────────────────────────

    public async Task<StaffTask?> GetTaskAsync(Guid id)
    {
        return await _db.StaffTask.FirstOrDefaultAsync(t => t.Id == id);
    }

    public void StageTask(StaffTask task)
    {
        _db.StaffTask.Add(task);
    }

    public async Task<(List<StaffTask> Items, int Total)> SearchTasksAsync(StaffTaskSearchCriteria criteria)
    {
        var query = _db.StaffTask.AsNoTracking().AsQueryable();
        if (criteria.TheaterIds != null)
        {
            var theaterIds = criteria.TheaterIds;
            query = query.Where(t => theaterIds.Contains(t.TheaterId));
        }
        if (criteria.AssignedToUserId.HasValue)
        {
            var assignedTo = criteria.AssignedToUserId.Value;
            query = query.Where(t => t.AssignedToUserId == assignedTo);
        }
        if (criteria.Status.HasValue)
        {
            var status = criteria.Status.Value;
            query = query.Where(t => t.Status == status);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreationTime)
            .ThenByDescending(t => t.Id)
            .Skip(criteria.PageIndex * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task<List<StaffTask>> GetUserTasksAsync(Guid userId, bool includeClosed)
    {
        var query = _db.StaffTask.AsNoTracking().Where(t => t.AssignedToUserId == userId);
        if (!includeClosed)
        {
            query = query.Where(t => t.Status == StaffTaskStatus.Open || t.Status == StaffTaskStatus.InProgress);
        }
        return await query
            .OrderBy(t => t.Status)
            .ThenBy(t => t.DueAt == null)
            .ThenBy(t => t.DueAt)
            .ThenByDescending(t => t.CreationTime)
            .Take(_maxUserTasks)
            .ToListAsync();
    }
}
