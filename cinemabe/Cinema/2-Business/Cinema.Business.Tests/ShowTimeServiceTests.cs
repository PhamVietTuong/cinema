using Cinema.Business.DTO.Catalog;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

/// <summary>
/// Covers the room-class ↔ projection-format guard (a 3D screening needs a 3D-capable room class)
/// and the room-class turnover buffer (a minimum gap enforced between showtimes in the same room).
/// </summary>
public class ShowTimeServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly ShowTimeManager _sut;

    private static readonly Guid MovieId = Guid.NewGuid();
    private static readonly Guid RoomId = Guid.NewGuid();

    public ShowTimeServiceTests()
    {
        _sut = new ShowTimeManager(_uowMock.Object);
        _uowMock.Setup(u => u.ShowTimeStore.HasRoomOverlapAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid?>()))
            .ReturnsAsync(false);
        // The Request() helper below builds a 2-hour window; 120 min keeps every pre-existing test
        // (which doesn't care about runtime at all) below the new floor by default.
        GivenMovie("Default Movie", duration: 120);
    }

    private void GivenRoomClass(
        string name, bool supportsThreeD, int turnoverBufferMinutes = 0,
        RoomStatus roomStatus = RoomStatus.Active, bool theaterIsActive = true, string theaterName = "Default Theater")
    {
        _uowMock.Setup(u => u.RoomStore.FindSelectAsync<ShowTimeManager.RoomClassInfo>(
                It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Room, object>>>()))
            .ReturnsAsync(new ShowTimeManager.RoomClassInfo(name, supportsThreeD, turnoverBufferMinutes, roomStatus, theaterName, theaterIsActive));
    }

    private void GivenMovie(string title, int duration, bool isActive = true, DateOnly? releaseDate = null, DateOnly? endDate = null)
    {
        _uowMock.Setup(u => u.MovieStore.FindSelectAsync<ShowTimeManager.MovieRuntimeInfo>(
                It.IsAny<System.Linq.Expressions.Expression<Func<Movie, bool>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Movie, object>>>()))
            .ReturnsAsync(new ShowTimeManager.MovieRuntimeInfo(
                title, duration, isActive, releaseDate ?? DateOnly.FromDateTime(DateTime.Now.AddYears(-1)), endDate));
    }

    private static CreateShowTimeRequest Request(ProjectionForm form, DateTime? start = null, DateTime? end = null)
    {
        return new CreateShowTimeRequest
        {
            MovieId        = MovieId,
            StartTime      = start ?? DateTime.Now.AddDays(1),
            EndTime        = end ?? (start ?? DateTime.Now.AddDays(1)).AddHours(2),
            ProjectionForm = form,
            RoomId         = RoomId,
            BasePrice      = 100,
        };
    }

    [Fact]
    public async Task CreateAsync_RejectsThreeD_WhenRoomClassHasNoThreeDProjector()
    {
        GivenRoomClass("Lagom", supportsThreeD: false);

        var act = async () => await _sut.CreateAsync(Request(ProjectionForm.ThreeD));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Lagom*cannot screen 3D*");
        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_AllowsTwoD_InARoomClassWithoutThreeD()
    {
        GivenRoomClass("Lagom", supportsThreeD: false);
        _uowMock.Setup(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>())).ReturnsAsync((ShowTime s) => s);

        await _sut.CreateAsync(Request(ProjectionForm.TwoD));

        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_AllowsThreeD_WhenRoomClassSupportsIt()
    {
        GivenRoomClass("IMAX", supportsThreeD: true);
        _uowMock.Setup(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>())).ReturnsAsync((ShowTime s) => s);

        await _sut.CreateAsync(Request(ProjectionForm.ThreeD));

        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_BehavesAsBefore_WhenBufferIsZero()
    {
        GivenRoomClass("Standard", supportsThreeD: false, turnoverBufferMinutes: 0);
        _uowMock.Setup(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>())).ReturnsAsync((ShowTime s) => s);

        await _sut.CreateAsync(Request(ProjectionForm.TwoD));

        _uowMock.Verify(u => u.ShowTimeStore.HasRoomOverlapAsync(
            RoomId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, null), Times.Once);
        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_PassesRoomClassBuffer_ToTheOverlapCheck()
    {
        GivenRoomClass("IMAX", supportsThreeD: true, turnoverBufferMinutes: 20);
        _uowMock.Setup(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>())).ReturnsAsync((ShowTime s) => s);

        await _sut.CreateAsync(Request(ProjectionForm.TwoD));

        _uowMock.Verify(u => u.ShowTimeStore.HasRoomOverlapAsync(
            RoomId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 20, null), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_RejectsShowTime_WhenGapToExistingIsSmallerThanBuffer()
    {
        GivenRoomClass("Standard", supportsThreeD: false, turnoverBufferMinutes: 20);
        _uowMock.Setup(u => u.ShowTimeStore.HasRoomOverlapAsync(
                RoomId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 20, It.IsAny<Guid?>()))
            .ReturnsAsync(true);

        var act = async () => await _sut.CreateAsync(Request(ProjectionForm.TwoD));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*20 minutes*turnover*");
        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ExcludesItself_FromBufferCheck()
    {
        var showTimeId = Guid.NewGuid();
        var existing = new ShowTime
        {
            Id             = showTimeId,
            MovieId        = MovieId,
            StartTime      = DateTime.Now.AddDays(1),
            EndTime        = DateTime.Now.AddDays(1).AddHours(2),
            ProjectionForm = ProjectionForm.TwoD,
        };
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdWithRoomsAsync(showTimeId)).ReturnsAsync(existing);
        GivenRoomClass("Standard", supportsThreeD: false, turnoverBufferMinutes: 20);

        var request = new UpdateShowTimeRequest
        {
            Id             = showTimeId,
            MovieId        = MovieId,
            StartTime      = existing.StartTime,
            EndTime        = existing.EndTime,
            ProjectionForm = ProjectionForm.TwoD,
            RoomId         = RoomId,
            BasePrice      = 120,
        };

        await _sut.UpdateAsync(request);

        _uowMock.Verify(u => u.ShowTimeStore.HasRoomOverlapAsync(
            RoomId, existing.StartTime, existing.EndTime, 20, showTimeId), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_RejectsWindowShorterThanMovieRuntime()
    {
        GivenMovie("Long Movie", duration: 150);
        GivenRoomClass("Standard", supportsThreeD: false);
        var start = DateTime.Now.AddDays(1);

        var act = async () => await _sut.CreateAsync(Request(ProjectionForm.TwoD, start, start.AddHours(2)));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Long Movie*150 minutes*");
        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_AllowsWindowLongerThanRuntime()
    {
        GivenMovie("Padded Movie", duration: 100);
        GivenRoomClass("Standard", supportsThreeD: false);
        _uowMock.Setup(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>())).ReturnsAsync((ShowTime s) => s);
        var start = DateTime.Now.AddDays(1);

        // 2-hour window, 100-min runtime: 20 minutes of trailers/ads stays legal.
        await _sut.CreateAsync(Request(ProjectionForm.TwoD, start, start.AddHours(2)));

        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_SkipsRuntimeCheck_WhenDurationIsZero()
    {
        GivenMovie("Unset Duration Movie", duration: 0);
        GivenRoomClass("Standard", supportsThreeD: false);
        _uowMock.Setup(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>())).ReturnsAsync((ShowTime s) => s);
        var start = DateTime.Now.AddDays(1);

        // Window shorter than any real runtime, but a 0 duration is a data gap the runtime floor
        // must not enforce against.
        await _sut.CreateAsync(Request(ProjectionForm.TwoD, start, start.AddMinutes(5)));

        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_RejectsWindowShorterThanMovieRuntime()
    {
        var showTimeId = Guid.NewGuid();
        var start = DateTime.Now.AddDays(1);
        var existing = new ShowTime
        {
            Id             = showTimeId,
            MovieId        = MovieId,
            StartTime      = start,
            EndTime        = start.AddHours(2),
            ProjectionForm = ProjectionForm.TwoD,
        };
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdWithRoomsAsync(showTimeId)).ReturnsAsync(existing);
        GivenMovie("Long Movie", duration: 150);

        var request = new UpdateShowTimeRequest
        {
            Id             = showTimeId,
            MovieId        = MovieId,
            StartTime      = start,
            EndTime        = start.AddHours(2),
            ProjectionForm = ProjectionForm.TwoD,
            RoomId         = Guid.Empty,
            BasePrice      = 100,
        };

        var act = async () => await _sut.UpdateAsync(request);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Long Movie*150 minutes*");
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    // ── Movie availability ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_RejectsShowTime_WhenMovieIsInactive()
    {
        GivenMovie("Retired Movie", duration: 120, isActive: false);
        GivenRoomClass("Standard", supportsThreeD: false);

        var act = async () => await _sut.CreateAsync(Request(ProjectionForm.TwoD));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Retired Movie*no longer showing*");
        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_RejectsShowTime_BeforeMovieReleaseDate()
    {
        var start = DateTime.Now.AddDays(1);
        GivenMovie("Future Movie", duration: 120, releaseDate: DateOnly.FromDateTime(start.AddDays(7)));
        GivenRoomClass("Standard", supportsThreeD: false);

        var act = async () => await _sut.CreateAsync(Request(ProjectionForm.TwoD, start));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Future Movie*not released until*");
        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_AllowsShowTime_OnTheReleaseDateItself()
    {
        var start = DateTime.Now.AddDays(1);
        GivenMovie("Opening Day Movie", duration: 120, releaseDate: DateOnly.FromDateTime(start));
        GivenRoomClass("Standard", supportsThreeD: false);
        _uowMock.Setup(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>())).ReturnsAsync((ShowTime s) => s);

        await _sut.CreateAsync(Request(ProjectionForm.TwoD, start));

        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_RejectsShowTime_AfterMovieEndDate()
    {
        var start = DateTime.Now.AddDays(1);
        GivenMovie("Finished Movie", duration: 120, endDate: DateOnly.FromDateTime(start.AddDays(-1)));
        GivenRoomClass("Standard", supportsThreeD: false);

        var act = async () => await _sut.CreateAsync(Request(ProjectionForm.TwoD, start));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Finished Movie*finished its run*");
        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_AllowsShowTime_OnTheEndDateItself()
    {
        var start = DateTime.Now.AddDays(1);
        GivenMovie("Closing Day Movie", duration: 120, endDate: DateOnly.FromDateTime(start));
        GivenRoomClass("Standard", supportsThreeD: false);
        _uowMock.Setup(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>())).ReturnsAsync((ShowTime s) => s);

        await _sut.CreateAsync(Request(ProjectionForm.TwoD, start));

        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_AllowsShowTime_WhenMovieHasNoEndDate()
    {
        var start = DateTime.Now.AddDays(400);
        GivenMovie("Evergreen Movie", duration: 120, endDate: null);
        GivenRoomClass("Standard", supportsThreeD: false);
        _uowMock.Setup(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>())).ReturnsAsync((ShowTime s) => s);

        await _sut.CreateAsync(Request(ProjectionForm.TwoD, start));

        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_SkipsMovieAvailability_ForAPastShowTime()
    {
        var showTimeId = Guid.NewGuid();
        var start = DateTime.Now.AddDays(-30);
        var existing = new ShowTime
        {
            Id             = showTimeId,
            MovieId        = MovieId,
            StartTime      = start,
            EndTime        = start.AddHours(2),
            ProjectionForm = ProjectionForm.TwoD,
        };
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdWithRoomsAsync(showTimeId)).ReturnsAsync(existing);
        GivenMovie("Now Retired Movie", duration: 120, isActive: false);

        var request = new UpdateShowTimeRequest
        {
            Id             = showTimeId,
            MovieId        = MovieId,
            StartTime      = start,
            EndTime        = start.AddHours(2),
            ProjectionForm = ProjectionForm.TwoD,
            RoomId         = Guid.Empty,
            BasePrice      = 100,
        };

        await _sut.UpdateAsync(request);

        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_RejectsInactiveMovie_ForAFutureShowTime()
    {
        var showTimeId = Guid.NewGuid();
        var start = DateTime.Now.AddDays(2);
        var existing = new ShowTime
        {
            Id             = showTimeId,
            MovieId        = MovieId,
            StartTime      = start,
            EndTime        = start.AddHours(2),
            ProjectionForm = ProjectionForm.TwoD,
        };
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdWithRoomsAsync(showTimeId)).ReturnsAsync(existing);
        GivenMovie("Newly Retired Movie", duration: 120, isActive: false);

        var request = new UpdateShowTimeRequest
        {
            Id             = showTimeId,
            MovieId        = MovieId,
            StartTime      = start,
            EndTime        = start.AddHours(2),
            ProjectionForm = ProjectionForm.TwoD,
            RoomId         = Guid.Empty,
            BasePrice      = 100,
        };

        var act = async () => await _sut.UpdateAsync(request);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Newly Retired Movie*no longer showing*");
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    // ── Room / theater availability ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_RejectsShowTime_WhenRoomIsUnderMaintenance()
    {
        GivenRoomClass("Standard", supportsThreeD: false, roomStatus: RoomStatus.Maintenance);

        var act = async () => await _sut.CreateAsync(Request(ProjectionForm.TwoD));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*maintenance*");
        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Never);
        _uowMock.Verify(u => u.ShowTimeStore.HasRoomOverlapAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_RejectsShowTime_WhenRoomIsInactive()
    {
        GivenRoomClass("Standard", supportsThreeD: false, roomStatus: RoomStatus.Inactive);

        var act = async () => await _sut.CreateAsync(Request(ProjectionForm.TwoD));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*not in service*");
        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_RejectsShowTime_WhenTheaterIsInactive()
    {
        GivenRoomClass("Standard", supportsThreeD: false, theaterIsActive: false, theaterName: "Old Plaza");

        var act = async () => await _sut.CreateAsync(Request(ProjectionForm.TwoD));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Old Plaza*closed*");
        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_RejectsTheaterFirst_WhenBothTheaterAndRoomAreInactive()
    {
        GivenRoomClass("Standard", supportsThreeD: false, roomStatus: RoomStatus.Inactive, theaterIsActive: false, theaterName: "Old Plaza");

        var act = async () => await _sut.CreateAsync(Request(ProjectionForm.TwoD));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Old Plaza*closed*");
        _uowMock.Verify(u => u.ShowTimeStore.HasRoomOverlapAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_SkipsRoomAvailability_ForAPastShowTime()
    {
        var showTimeId = Guid.NewGuid();
        var start = DateTime.Now.AddDays(-30);
        var existing = new ShowTime
        {
            Id             = showTimeId,
            MovieId        = MovieId,
            StartTime      = start,
            EndTime        = start.AddHours(2),
            ProjectionForm = ProjectionForm.TwoD,
        };
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdWithRoomsAsync(showTimeId)).ReturnsAsync(existing);
        GivenRoomClass("Standard", supportsThreeD: false, roomStatus: RoomStatus.Inactive);

        var request = new UpdateShowTimeRequest
        {
            Id             = showTimeId,
            MovieId        = MovieId,
            StartTime      = start,
            EndTime        = start.AddHours(2),
            ProjectionForm = ProjectionForm.TwoD,
            RoomId         = RoomId,
            BasePrice      = 100,
        };

        await _sut.UpdateAsync(request);

        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }
}
