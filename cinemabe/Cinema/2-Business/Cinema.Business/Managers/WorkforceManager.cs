using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
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

    // ── Time clock ───────────────────────────────────────────────────────────

    public async Task<TimeClockEntryDTO> ClockInAsync(Guid theaterId, Guid userId, ClockInRequest request)
    {
        if (await _uow.WorkforceStore.GetOpenClockEntryAsync(userId) != null)
        {
            throw new InvalidOperationException("You are already clocked in.");
        }

        var entry = new TimeClockEntry
        {
            TheaterId = theaterId,
            UserId = userId,
            ClockInAt = DateTime.UtcNow,
            Note = request.Note?.Trim()
        };
        try
        {
            await _uow.WorkforceStore.AddClockEntryAsync(entry);
        }
        catch (Exception)
        {
            // A concurrent clock-in won the race: the unique open-entry index rejected this one.
            if (await _uow.WorkforceStore.GetOpenClockEntryAsync(userId) != null)
            {
                throw new InvalidOperationException("You are already clocked in.");
            }
            throw;
        }
        return ToClockDto(entry, null);
    }

    public async Task<TimeClockEntryDTO> ClockOutAsync(Guid userId, ClockOutRequest request)
    {
        var entry = await _uow.WorkforceStore.GetOpenClockEntryAsync(userId);
        if (entry == null)
        {
            throw new InvalidOperationException("You are not clocked in.");
        }

        entry.ClockOutAt = DateTime.UtcNow;
        entry.LastUpdatedTime = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            entry.Note = request.Note.Trim();
        }
        await _uow.SaveChangesAsync();
        return ToClockDto(entry, null);
    }

    public async Task<ClockStatusDTO> GetMyClockStatusAsync(Guid userId)
    {
        var entry = await _uow.WorkforceStore.GetOpenClockEntryAsync(userId);
        if (entry == null)
        {
            return new ClockStatusDTO { IsClockedIn = false };
        }
        return new ClockStatusDTO { IsClockedIn = true, OpenEntry = ToClockDto(entry, null) };
    }

    public async Task<List<TimeClockEntryDTO>> GetTimeSheetAsync(Guid theaterId, TimeSheetRequest request)
    {
        EnsureRange(request.From, request.To);
        var entries = await _uow.WorkforceStore.GetClockEntriesAsync(theaterId, request.From, request.To, request.UserId);
        var names = await _uow.UserStore.GetNamesByIdsAsync(entries.Select(e => e.UserId).Distinct().ToList());
        return entries.Select(e =>
        {
            names.TryGetValue(e.UserId, out var name);
            return ToClockDto(e, name);
        }).ToList();
    }

    // ── Tasks ────────────────────────────────────────────────────────────────

    public async Task<StaffTaskDTO> SaveTaskAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid theaterId, Guid actorUserId, SaveStaffTaskRequest request)
    {
        await EnsureStaffOfTheaterAsync(theaterId, request.AssignedToUserId);
        await EnsureLinksInTheaterAsync(theaterId, request.IncidentId, request.ChecklistRunId);

        StaffTask task;
        if (request.Id.HasValue)
        {
            var existing = await _uow.WorkforceStore.GetTaskAsync(request.Id.Value);
            if (existing == null)
            {
                throw new KeyNotFoundException("Task not found.");
            }
            EnsureInScope(scopeTheaterIds, existing.TheaterId);
            if (existing.TheaterId != theaterId)
            {
                throw new InvalidOperationException("The task belongs to another theater.");
            }
            task = existing;
            task.LastUpdatedTime = DateTime.UtcNow;
            if (request.Status.HasValue)
            {
                ApplyStatus(task, request.Status.Value);
            }
        }
        else
        {
            task = new StaffTask { TheaterId = theaterId, CreatedByUserId = actorUserId };
            _uow.WorkforceStore.StageTask(task);
        }

        task.AssignedToUserId = request.AssignedToUserId;
        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.DueAt = request.DueAt;
        task.IncidentId = request.IncidentId;
        task.ChecklistRunId = request.ChecklistRunId;
        await _uow.SaveChangesAsync();

        return (await ToTaskDtosAsync(new List<StaffTask> { task }))[0];
    }

    public async Task<DefaultSearchResults<StaffTaskDTO>> GetTasksAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var criteria = new StaffTaskSearchCriteria(
            TheaterIds: scopeTheaterIds,
            AssignedToUserId: search.Filters.GetGuid("assignedTo"),
            Status: search.Filters.GetEnum<StaffTaskStatus>("status"),
            PageIndex: page - 1,
            PageSize: pageSize);

        var (tasks, total) = await _uow.WorkforceStore.SearchTasksAsync(criteria);
        return new DefaultSearchResults<StaffTaskDTO>
        {
            Results = await ToTaskDtosAsync(tasks),
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page
        };
    }

    public async Task<List<StaffTaskDTO>> GetMyTasksAsync(Guid userId, MyTasksRequest request)
    {
        var tasks = await _uow.WorkforceStore.GetUserTasksAsync(userId, request.IncludeClosed);
        return await ToTaskDtosAsync(tasks);
    }

    public async Task<StaffTaskDTO> SetMyTaskStatusAsync(Guid userId, SetMyTaskStatusRequest request)
    {
        var task = await _uow.WorkforceStore.GetTaskAsync(request.TaskId);
        if (task == null)
        {
            throw new KeyNotFoundException("Task not found.");
        }
        if (task.AssignedToUserId != userId)
        {
            throw new AccessDeniedException("This task is assigned to someone else.");
        }
        if (task.Status == StaffTaskStatus.Cancelled)
        {
            throw new InvalidOperationException("The task was cancelled.");
        }
        if (request.Status == StaffTaskStatus.Cancelled)
        {
            throw new InvalidOperationException("Only a manager can cancel a task.");
        }

        ApplyStatus(task, request.Status);
        task.LastUpdatedTime = DateTime.UtcNow;
        await _uow.SaveChangesAsync();
        return (await ToTaskDtosAsync(new List<StaffTask> { task }))[0];
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void ApplyStatus(StaffTask task, StaffTaskStatus status)
    {
        task.Status = status;
        if (status == StaffTaskStatus.Done)
        {
            task.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            task.CompletedAt = null;
        }
    }

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

    private async Task EnsureLinksInTheaterAsync(Guid theaterId, Guid? incidentId, Guid? checklistRunId)
    {
        if (incidentId.HasValue)
        {
            var incident = await _uow.IncidentStore.GetByIdAsync(incidentId.Value);
            if (incident == null)
            {
                throw new KeyNotFoundException("Incident not found.");
            }
            if (incident.TheaterId != theaterId)
            {
                throw new AccessDeniedException("The incident belongs to another theater.");
            }
        }
        if (checklistRunId.HasValue)
        {
            var run = await _uow.ChecklistStore.GetRunByIdAsync(checklistRunId.Value);
            if (run == null)
            {
                throw new KeyNotFoundException("Checklist not found.");
            }
            if (run.TheaterId != theaterId)
            {
                throw new AccessDeniedException("The checklist belongs to another theater.");
            }
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

    private async Task<List<StaffTaskDTO>> ToTaskDtosAsync(List<StaffTask> tasks)
    {
        var userIds = tasks.Select(t => t.AssignedToUserId).Concat(tasks.Select(t => t.CreatedByUserId)).Distinct().ToList();
        var names = await _uow.UserStore.GetNamesByIdsAsync(userIds);
        return tasks.Select(t =>
        {
            names.TryGetValue(t.AssignedToUserId, out var assignedName);
            names.TryGetValue(t.CreatedByUserId, out var createdName);
            return new StaffTaskDTO
            {
                Id = t.Id,
                TheaterId = t.TheaterId,
                AssignedToUserId = t.AssignedToUserId,
                AssignedToName = assignedName,
                CreatedByUserId = t.CreatedByUserId,
                CreatedByName = createdName,
                Title = t.Title,
                Description = t.Description,
                DueAt = t.DueAt,
                Status = t.Status,
                CompletedAt = t.CompletedAt,
                IncidentId = t.IncidentId,
                ChecklistRunId = t.ChecklistRunId,
                CreationTime = t.CreationTime
            };
        }).ToList();
    }

    private static TimeClockEntryDTO ToClockDto(TimeClockEntry entry, string? userName)
    {
        var end = entry.ClockOutAt;
        if (end == null)
        {
            end = DateTime.UtcNow;
        }
        return new TimeClockEntryDTO
        {
            Id = entry.Id,
            TheaterId = entry.TheaterId,
            UserId = entry.UserId,
            UserName = userName,
            ClockInAt = entry.ClockInAt,
            ClockOutAt = entry.ClockOutAt,
            DurationMinutes = (int)Math.Max(0, (end.Value - entry.ClockInAt).TotalMinutes),
            Note = entry.Note
        };
    }
}
