using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class WorkforceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly WorkforceManager _sut;
    private readonly Guid _theaterId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly List<TimeClockEntry> _entries = new();
    private readonly List<StaffShift> _stagedShifts = new();
    private readonly List<StaffTask> _stagedTasks = new();

    public WorkforceTests()
    {
        _sut = new WorkforceManager(_uowMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.UserStore.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>())).ReturnsAsync(new Dictionary<Guid, string>());
        _uowMock.Setup(u => u.WorkforceStore.StageShift(It.IsAny<StaffShift>())).Callback<StaffShift>(s => _stagedShifts.Add(s));
        _uowMock.Setup(u => u.WorkforceStore.StageTask(It.IsAny<StaffTask>())).Callback<StaffTask>(t => _stagedTasks.Add(t));
        // A fake clock store: an entry with no ClockOutAt is the user's single open entry.
        _uowMock.Setup(u => u.WorkforceStore.GetOpenClockEntryAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _entries.FirstOrDefault(e => e.UserId == id && e.ClockOutAt == null));
        _uowMock.Setup(u => u.WorkforceStore.AddClockEntryAsync(It.IsAny<TimeClockEntry>()))
            .Callback<TimeClockEntry>(e => _entries.Add(e))
            .Returns(Task.CompletedTask);
    }

    private User GivenStaff(Guid? theaterId = null, UserStatus status = UserStatus.Active)
    {
        var user = new User { Id = Guid.NewGuid(), Name = "Staff", TheaterId = theaterId ?? _theaterId, Status = status };
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(user.Id)).ReturnsAsync(user);
        return user;
    }

    // ── Time clock ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ClockIn_CreatesOpenEntry()
    {
        var entry = await _sut.ClockInAsync(_theaterId, _userId, new ClockInRequest { Note = " opening " });

        _entries.Should().ContainSingle();
        entry.ClockOutAt.Should().BeNull();
        entry.Note.Should().Be("opening");
        (await _sut.GetMyClockStatusAsync(_userId)).IsClockedIn.Should().BeTrue();
    }

    [Fact]
    public async Task ClockIn_Twice_Is400_AndCreatesNoSecondEntry()
    {
        await _sut.ClockInAsync(_theaterId, _userId, new ClockInRequest());

        var act = () => _sut.ClockInAsync(_theaterId, _userId, new ClockInRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
        _entries.Should().ContainSingle();
    }

    [Fact]
    public async Task ClockIn_LosingTheRaceToTheUniqueIndex_Is400()
    {
        _uowMock.SetupSequence(u => u.WorkforceStore.GetOpenClockEntryAsync(_userId))
            .ReturnsAsync((TimeClockEntry?)null)
            .ReturnsAsync(new TimeClockEntry { UserId = _userId });
        _uowMock.Setup(u => u.WorkforceStore.AddClockEntryAsync(It.IsAny<TimeClockEntry>())).ThrowsAsync(new Exception("duplicate key"));

        var act = () => _sut.ClockInAsync(_theaterId, _userId, new ClockInRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ClockOut_ClosesTheOpenEntry_AndAllowsANewClockIn()
    {
        await _sut.ClockInAsync(_theaterId, _userId, new ClockInRequest());

        var closed = await _sut.ClockOutAsync(_userId, new ClockOutRequest());

        closed.ClockOutAt.Should().NotBeNull();
        (await _sut.GetMyClockStatusAsync(_userId)).IsClockedIn.Should().BeFalse();
        await _sut.ClockInAsync(_theaterId, _userId, new ClockInRequest());
        _entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task ClockOut_WhenNotClockedIn_Is400()
    {
        var act = () => _sut.ClockOutAsync(_userId, new ClockOutRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TimeSheet_ReportsWorkedMinutes_AndRejectsRangesOver31Days()
    {
        var start = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        _uowMock.Setup(u => u.WorkforceStore.GetClockEntriesAsync(_theaterId, start, start.AddDays(7), null))
            .ReturnsAsync(new List<TimeClockEntry>
            {
                new() { TheaterId = _theaterId, UserId = _userId, ClockInAt = start, ClockOutAt = start.AddMinutes(95) }
            });

        var sheet = await _sut.GetTimeSheetAsync(_theaterId, new TimeSheetRequest { From = start, To = start.AddDays(7) });

        sheet.Should().ContainSingle().Which.DurationMinutes.Should().Be(95);
        var tooLong = () => _sut.GetTimeSheetAsync(_theaterId, new TimeSheetRequest { From = start, To = start.AddDays(40) });
        await tooLong.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Roster ───────────────────────────────────────────────────────────────

    private SaveStaffShiftRequest ShiftRequest(Guid userId, int startHour = 9, int hours = 8)
    {
        var start = new DateTime(2026, 10, 5, startHour, 0, 0);
        return new SaveStaffShiftRequest { UserId = userId, StartTime = start, EndTime = start.AddHours(hours) };
    }

    [Fact]
    public async Task SaveShift_NewShift_IsStaged_ForAStaffMemberOfTheTheater()
    {
        var staff = GivenStaff();

        var dto = await _sut.SaveShiftAsync(new[] { _theaterId }, _theaterId, ShiftRequest(staff.Id));

        _stagedShifts.Should().ContainSingle().Which.TheaterId.Should().Be(_theaterId);
        dto.UserId.Should().Be(staff.Id);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SaveShift_StaffOfAnotherTheater_Is400()
    {
        var other = GivenStaff(Guid.NewGuid());

        var act = () => _sut.SaveShiftAsync(null, _theaterId, ShiftRequest(other.Id));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _stagedShifts.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveShift_Overlapping_Is400()
    {
        var staff = GivenStaff();
        _uowMock.Setup(u => u.WorkforceStore.HasShiftOverlapAsync(staff.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).ReturnsAsync(true);

        var act = () => _sut.SaveShiftAsync(null, _theaterId, ShiftRequest(staff.Id));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveShift_EndBeforeStartOrTooLong_Is400()
    {
        var staff = GivenStaff();
        var backwards = ShiftRequest(staff.Id);
        backwards.EndTime = backwards.StartTime.AddHours(-1);

        await ((Func<Task>)(() => _sut.SaveShiftAsync(null, _theaterId, backwards))).Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)(() => _sut.SaveShiftAsync(null, _theaterId, ShiftRequest(staff.Id, hours: 20)))).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveShift_EditingAShiftOutsideScope_Is403()
    {
        var staff = GivenStaff();
        var shift = new StaffShift { Id = Guid.NewGuid(), TheaterId = _theaterId, UserId = staff.Id };
        _uowMock.Setup(u => u.WorkforceStore.GetShiftAsync(shift.Id)).ReturnsAsync(shift);
        var request = ShiftRequest(staff.Id);
        request.Id = shift.Id;

        var act = () => _sut.SaveShiftAsync(new[] { Guid.NewGuid() }, _theaterId, request);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task DeleteShift_StagesRemoval_AndRefusesOutOfScope()
    {
        var shift = new StaffShift { Id = Guid.NewGuid(), TheaterId = _theaterId };
        _uowMock.Setup(u => u.WorkforceStore.GetShiftAsync(shift.Id)).ReturnsAsync(shift);

        var outOfScope = () => _sut.DeleteShiftAsync(new[] { Guid.NewGuid() }, new DeleteStaffShiftRequest { ShiftId = shift.Id });
        await outOfScope.Should().ThrowAsync<AccessDeniedException>();

        await _sut.DeleteShiftAsync(new[] { _theaterId }, new DeleteStaffShiftRequest { ShiftId = shift.Id });
        _uowMock.Verify(u => u.WorkforceStore.StageDeleteShift(shift), Times.Once);
    }

    [Fact]
    public async Task Roster_RangeOver31Days_Is400()
    {
        var from = new DateTime(2026, 10, 1);

        var act = () => _sut.GetRosterAsync(_theaterId, new RosterRequest { From = from, To = from.AddDays(45) });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Tasks ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveTask_LinkedToIncident_IsAssignedAndStartsOpen()
    {
        var staff = GivenStaff();
        var incident = new Incident { Id = Guid.NewGuid(), TheaterId = _theaterId };
        _uowMock.Setup(u => u.IncidentStore.GetByIdAsync(incident.Id)).ReturnsAsync(incident);
        var manager = Guid.NewGuid();

        var dto = await _sut.SaveTaskAsync(null, _theaterId, manager, new SaveStaffTaskRequest
        {
            AssignedToUserId = staff.Id,
            Title = " Replace seat A1 ",
            IncidentId = incident.Id
        });

        _stagedTasks.Should().ContainSingle();
        dto.Status.Should().Be(StaffTaskStatus.Open);
        dto.Title.Should().Be("Replace seat A1");
        dto.IncidentId.Should().Be(incident.Id);
        dto.CreatedByUserId.Should().Be(manager);
    }

    [Fact]
    public async Task SaveTask_LinkedToAnotherTheatersChecklistRun_Is403()
    {
        var staff = GivenStaff();
        var run = new ChecklistRun { Id = Guid.NewGuid(), TheaterId = Guid.NewGuid() };
        _uowMock.Setup(u => u.ChecklistStore.GetRunByIdAsync(run.Id)).ReturnsAsync(run);

        var act = () => _sut.SaveTaskAsync(null, _theaterId, Guid.NewGuid(), new SaveStaffTaskRequest
        {
            AssignedToUserId = staff.Id,
            Title = "x",
            ChecklistRunId = run.Id
        });

        await act.Should().ThrowAsync<AccessDeniedException>();
        _stagedTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task SetMyTaskStatus_OwnTask_MovesToDone_AndStampsCompletion()
    {
        var task = new StaffTask { Id = Guid.NewGuid(), TheaterId = _theaterId, AssignedToUserId = _userId, Title = "t" };
        _uowMock.Setup(u => u.WorkforceStore.GetTaskAsync(task.Id)).ReturnsAsync(task);

        var dto = await _sut.SetMyTaskStatusAsync(_userId, new SetMyTaskStatusRequest { TaskId = task.Id, Status = StaffTaskStatus.Done });

        dto.Status.Should().Be(StaffTaskStatus.Done);
        task.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SetMyTaskStatus_SomeoneElsesTask_Is403_AndCancelIsRefused()
    {
        var task = new StaffTask { Id = Guid.NewGuid(), TheaterId = _theaterId, AssignedToUserId = Guid.NewGuid(), Title = "t" };
        _uowMock.Setup(u => u.WorkforceStore.GetTaskAsync(task.Id)).ReturnsAsync(task);

        var notMine = () => _sut.SetMyTaskStatusAsync(_userId, new SetMyTaskStatusRequest { TaskId = task.Id, Status = StaffTaskStatus.Done });
        await notMine.Should().ThrowAsync<AccessDeniedException>();

        task.AssignedToUserId = _userId;
        var cancel = () => _sut.SetMyTaskStatusAsync(_userId, new SetMyTaskStatusRequest { TaskId = task.Id, Status = StaffTaskStatus.Cancelled });
        await cancel.Should().ThrowAsync<InvalidOperationException>();
    }
}
