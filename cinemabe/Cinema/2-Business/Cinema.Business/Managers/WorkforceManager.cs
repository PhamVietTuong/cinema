using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Staff;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class WorkforceManager : IWorkforceManager
{
    private const int _maxRangeDays = 31;
    private static readonly TimeSpan _maxShiftLength = TimeSpan.FromHours(16);
    private static readonly string[] _theaterScopedRoles = RoleNames.TheaterScopedRoles.Split(',');

    private readonly IApplicationUnitOfWork _uow;

    public WorkforceManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    // ── Roster ───────────────────────────────────────────────────────────────

    public async Task<List<TheaterStaffDTO>> GetTheaterStaffAsync(Guid theaterId)
    {
        var staff = await _uow.WorkforceStore.GetTheaterStaffAsync(theaterId, _theaterScopedRoles);
        return staff.Select(s => new TheaterStaffDTO { Id = s.Id, Name = s.Name, RoleName = s.RoleName }).ToList();
    }

    public async Task<List<StaffShiftDTO>> GetRosterAsync(Guid theaterId, RosterRequest request)
    {
        EnsureRange(request.From, request.To);
        var shifts = await _uow.WorkforceStore.GetShiftsAsync(theaterId, request.From, request.To, request.UserId);
        return await ToShiftDtosAsync(shifts);
    }

    public async Task<List<StaffShiftDTO>> GetMyShiftsAsync(Guid userId, MyShiftsRequest request)
    {
        EnsureRange(request.From, request.To);
        var shifts = await _uow.WorkforceStore.GetUserShiftsAsync(userId, request.From, request.To);
        return await ToShiftDtosAsync(shifts);
    }

    public async Task<StaffShiftDTO> SaveShiftAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid theaterId, SaveStaffShiftRequest request)
    {
        if (request.EndTime <= request.StartTime)
        {
            throw new InvalidOperationException("The shift must end after it starts.");
        }
        if (request.EndTime - request.StartTime > _maxShiftLength)
        {
            throw new InvalidOperationException($"A shift can last at most {_maxShiftLength.TotalHours:0} hours.");
        }
        await EnsureStaffOfTheaterAsync(theaterId, request.UserId);

        StaffShift shift;
        if (request.Id.HasValue)
        {
            var existing = await _uow.WorkforceStore.GetShiftAsync(request.Id.Value);
            if (existing == null)
            {
                throw new KeyNotFoundException("Shift not found.");
            }
            EnsureInScope(scopeTheaterIds, existing.TheaterId);
            if (existing.TheaterId != theaterId)
            {
                throw new InvalidOperationException("The shift belongs to another theater.");
            }
            shift = existing;
        }
        else
        {
            shift = new StaffShift { TheaterId = theaterId };
        }

        if (await _uow.WorkforceStore.HasShiftOverlapAsync(request.UserId, request.StartTime, request.EndTime, request.Id))
        {
            throw new InvalidOperationException("This person already has a shift that overlaps these hours.");
        }

        shift.UserId = request.UserId;
        shift.StartTime = request.StartTime;
        shift.EndTime = request.EndTime;
        shift.Note = request.Note?.Trim();
        if (request.Id.HasValue)
        {
            shift.LastUpdatedTime = DateTime.UtcNow;
        }
        else
        {
            _uow.WorkforceStore.StageShift(shift);
        }
        await _uow.SaveChangesAsync();

        return (await ToShiftDtosAsync(new List<StaffShift> { shift }))[0];
    }

    public async Task DeleteShiftAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, DeleteStaffShiftRequest request)
    {
        var shift = await _uow.WorkforceStore.GetShiftAsync(request.ShiftId);
        if (shift == null)
        {
            throw new KeyNotFoundException("Shift not found.");
        }
        EnsureInScope(scopeTheaterIds, shift.TheaterId);
        _uow.WorkforceStore.StageDeleteShift(shift);
        await _uow.SaveChangesAsync();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void EnsureRange(DateTime from, DateTime to)
    {
        if (to <= from)
        {
            throw new InvalidOperationException("The end of the range must be after its start.");
        }
        if ((to - from).TotalDays > _maxRangeDays)
        {
            throw new InvalidOperationException($"The range can span at most {_maxRangeDays} days.");
        }
    }

    private static void EnsureInScope(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid theaterId)
    {
        if (scopeTheaterIds != null && !scopeTheaterIds.Contains(theaterId))
        {
            throw new AccessDeniedException("The record is outside your scope.");
        }
    }

    private async Task EnsureStaffOfTheaterAsync(Guid theaterId, Guid userId)
    {
        var user = await _uow.UserStore.GetByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("Staff member not found.");
        }
        if (user.TheaterId != theaterId || user.Status != UserStatus.Active)
        {
            throw new InvalidOperationException("That person is not an active staff member of this theater.");
        }
    }

    private async Task<List<StaffShiftDTO>> ToShiftDtosAsync(List<StaffShift> shifts)
    {
        var names = await _uow.UserStore.GetNamesByIdsAsync(shifts.Select(s => s.UserId).Distinct().ToList());
        return shifts.Select(s =>
        {
            names.TryGetValue(s.UserId, out var name);
            return new StaffShiftDTO
            {
                Id = s.Id,
                TheaterId = s.TheaterId,
                UserId = s.UserId,
                UserName = name,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Note = s.Note
            };
        }).ToList();
    }

}
