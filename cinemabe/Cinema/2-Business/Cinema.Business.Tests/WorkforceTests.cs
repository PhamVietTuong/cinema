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
    private readonly List<StaffShift> _stagedShifts = new();

    public WorkforceTests()
    {
        _sut = new WorkforceManager(_uowMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.UserStore.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>())).ReturnsAsync(new Dictionary<Guid, string>());
        _uowMock.Setup(u => u.WorkforceStore.StageShift(It.IsAny<StaffShift>())).Callback<StaffShift>(s => _stagedShifts.Add(s));
    }

    private User GivenStaff(Guid? theaterId = null, UserStatus status = UserStatus.Active)
    {
        var user = new User { Id = Guid.NewGuid(), Name = "Staff", TheaterId = theaterId ?? _theaterId, Status = status };
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(user.Id)).ReturnsAsync(user);
        return user;
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
}
