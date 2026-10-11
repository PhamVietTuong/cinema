using System.Linq.Expressions;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class RoomSeatMapTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly RoomManager _sut;
    private readonly Room _room;
    private readonly List<Seat> _seats = new();
    private readonly List<Guid> _deletedSeatIds = new();
    private readonly List<(Guid RoomId, string RowName, int AfterColIndex)> _shiftCalls = new();

    public RoomSeatMapTests()
    {
        _sut = new RoomManager(_uowMock.Object);
        _room = new Room { Id = Guid.NewGuid(), Name = "Room 1", TheaterId = Guid.NewGuid(), TotalRows = 1, TotalColumns = 3 };
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(_room.Id)).ReturnsAsync(_room);
        _uowMock.Setup(u => u.RoomStore.UpdateAsync(It.IsAny<Room>())).ReturnsAsync((Room r) => r);

        // Default: no upcoming showtimes and no ticket history, unless a test overrides it.
        _uowMock.Setup(u => u.ShowTimeStore.ExistsAsync(It.IsAny<Expression<Func<ShowTime, bool>>>())).ReturnsAsync(false);
        _uowMock.Setup(u => u.SeatStore.HasAnyTicketAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        _uowMock.Setup(u => u.SeatStore.FindAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .ReturnsAsync((Expression<Func<Seat, bool>> predicate) => _seats.Where(predicate.Compile()).ToList());
        _uowMock.Setup(u => u.SeatStore.CountAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .ReturnsAsync((Expression<Func<Seat, bool>> predicate) => _seats.Count(predicate.Compile()));
        _uowMock.Setup(u => u.SeatStore.GetSeatMapByRoomNoTrackingAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid roomId) => _seats.Where(s => s.RoomId == roomId).OrderBy(s => s.RowName).ThenBy(s => s.ColIndex).ToList());
        _uowMock.Setup(u => u.SeatStore.DeleteAsync(It.IsAny<Seat>()))
            .Callback<Seat>(seat => { _seats.Remove(seat); _deletedSeatIds.Add(seat.Id); })
            .ReturnsAsync((Seat seat) => seat);
        _uowMock.Setup(u => u.SeatStore.ShiftRowLeftAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()))
            .Callback<Guid, string, int>((roomId, rowName, afterColIndex) =>
            {
                _shiftCalls.Add((roomId, rowName, afterColIndex));
                foreach (var s in _seats.Where(s => s.RoomId == roomId && s.RowName == rowName && s.ColIndex > afterColIndex))
                {
                    s.ColIndex--;
                }
            })
            .ReturnsAsync(0);
    }

    private Seat GivenSeat(string row, int col, Guid? groupId = null)
    {
        var seat = new Seat { Id = Guid.NewGuid(), RoomId = _room.Id, RowName = row, ColIndex = col, SeatGroupId = groupId, IsActive = true };
        _seats.Add(seat);
        return seat;
    }

    // ── Guards ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveSeat_RoomMissing_Throws()
    {
        var request = new RemoveSeatRequest { RoomId = Guid.NewGuid(), SeatId = Guid.NewGuid() };

        var act = () => _sut.RemoveSeatAsync(request);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RemoveSeat_SeatNotInRoom_Throws()
    {
        var request = new RemoveSeatRequest { RoomId = _room.Id, SeatId = Guid.NewGuid() };

        var act = () => _sut.RemoveSeatAsync(request);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RemoveSeat_RoomHasUpcomingShowTime_Throws_AndDoesNotDeleteOrShift()
    {
        GivenSeat("F", 1);
        var seat2 = GivenSeat("F", 2);
        GivenSeat("F", 3);
        _uowMock.Setup(u => u.ShowTimeStore.ExistsAsync(It.IsAny<Expression<Func<ShowTime, bool>>>())).ReturnsAsync(true);

        var act = () => _sut.RemoveSeatAsync(new RemoveSeatRequest { RoomId = _room.Id, SeatId = seat2.Id });

        await act.Should().ThrowAsync<InvalidOperationException>();
        _deletedSeatIds.Should().BeEmpty();
        _shiftCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveSeat_SeatHasTicketHistory_Throws()
    {
        GivenSeat("F", 1);
        var seat2 = GivenSeat("F", 2);
        GivenSeat("F", 3);
        _uowMock.Setup(u => u.SeatStore.HasAnyTicketAsync(seat2.Id)).ReturnsAsync(true);

        var act = () => _sut.RemoveSeatAsync(new RemoveSeatRequest { RoomId = _room.Id, SeatId = seat2.Id });

        await act.Should().ThrowAsync<InvalidOperationException>();
        _deletedSeatIds.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveSeat_LastSeatInRow_Throws()
    {
        var onlySeat = GivenSeat("F", 1);

        var act = () => _sut.RemoveSeatAsync(new RemoveSeatRequest { RoomId = _room.Id, SeatId = onlySeat.Id });

        await act.Should().ThrowAsync<InvalidOperationException>();
        _deletedSeatIds.Should().BeEmpty();
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveSeat_StandardSeat_DeletesAndShiftsRow_LeavesTotalColumnsUnchanged()
    {
        var f1 = GivenSeat("F", 1);
        var f2 = GivenSeat("F", 2);
        var f3 = GivenSeat("F", 3);

        var result = await _sut.RemoveSeatAsync(new RemoveSeatRequest { RoomId = _room.Id, SeatId = f2.Id });

        _deletedSeatIds.Should().ContainSingle().Which.Should().Be(f2.Id);
        _shiftCalls.Should().ContainSingle(c => c.RoomId == _room.Id && c.RowName == "F" && c.AfterColIndex == 2);
        _room.TotalColumns.Should().Be(3);
        result.Select(s => (s.Id, s.ColIndex)).Should().BeEquivalentTo(new[] { (f1.Id, 1), (f3.Id, 2) });
    }

    [Fact]
    public async Task RemoveSeat_HalfOfDoubleSeat_UnpairsThePartner()
    {
        var groupId = Guid.NewGuid();
        GivenSeat("F", 1);
        var f2 = GivenSeat("F", 2, groupId);
        var f3 = GivenSeat("F", 3, groupId);

        await _sut.RemoveSeatAsync(new RemoveSeatRequest { RoomId = _room.Id, SeatId = f2.Id });

        f3.SeatGroupId.Should().BeNull();
    }

    [Fact]
    public async Task RemoveSeat_PartnerLeftWithAMalformedGroup_ReadsBackAsStandard()
    {
        // Simulates stale/bad data: the "partner" doesn't actually share seat2's SeatGroupId, so the
        // removal can't find and unpair it. GetSeatMapAsync must still not report it as a broken double.
        var seat1 = GivenSeat("F", 1);
        var seat2 = GivenSeat("F", 2, Guid.NewGuid());
        var orphanPartner = GivenSeat("F", 3, Guid.NewGuid()); // different group id than seat2 — not a real pair
        GivenSeat("F", 4);

        var result = await _sut.RemoveSeatAsync(new RemoveSeatRequest { RoomId = _room.Id, SeatId = seat1.Id });

        var returnedPartner = result.Single(s => s.Id == orphanPartner.Id);
        returnedPartner.IsDouble.Should().BeFalse();
        returnedPartner.SeatGroupId.Should().BeNull();
    }

    [Fact]
    public async Task RemoveSeat_CommitsTheTransaction()
    {
        GivenSeat("F", 1);
        var f2 = GivenSeat("F", 2);
        GivenSeat("F", 3);

        await _sut.RemoveSeatAsync(new RemoveSeatRequest { RoomId = _room.Id, SeatId = f2.Id });

        _uowMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Never);
    }

    // ── Resize, per-row semantics ────────────────────────────────────────────

    [Fact]
    public async Task ResizeGrid_AddColumn_OnlyAppendsToEachRowsOwnLength_DoesNotResurrectARemovedSeat()
    {
        // Rows are named "A", "B", ... by index (ToRowName), two rows requested below.
        // Row A was shortened to 2 seats by an earlier single-seat removal; row B still has the full 3.
        GivenSeat("A", 1);
        GivenSeat("A", 2);
        GivenSeat("B", 1);
        GivenSeat("B", 2);
        GivenSeat("B", 3);
        var created = new List<Seat>();
        _uowMock.Setup(u => u.SeatStore.CreateRangeAsync(It.IsAny<List<Seat>>()))
            .Callback<List<Seat>>(created.AddRange)
            .ReturnsAsync((List<Seat> list) => list);
        _uowMock.Setup(u => u.SeatStore.DeleteAsync(It.IsAny<Expression<Func<Seat, bool>>>())).ReturnsAsync(true);

        await _sut.ResizeSeatGridAsync(new ResizeSeatGridRequest { RoomId = _room.Id, TotalRows = 2, TotalColumns = 4 });

        created.Should().HaveCount(3);
        created.Should().ContainSingle(s => s.RowName == "A" && s.ColIndex == 3);
        created.Should().ContainSingle(s => s.RowName == "A" && s.ColIndex == 4);
        created.Should().ContainSingle(s => s.RowName == "B" && s.ColIndex == 4);
    }

    [Fact]
    public async Task ResizeGrid_RemoveColumn_RemovesOnlyTheLastSeatOfEachRow()
    {
        GivenSeat("A", 1);
        GivenSeat("A", 2);
        GivenSeat("B", 1);
        GivenSeat("B", 2);
        GivenSeat("B", 3);
        var deletedIds = new List<Guid>();
        _uowMock.Setup(u => u.SeatStore.DeleteAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .Callback<Expression<Func<Seat, bool>>>(predicate => deletedIds.AddRange(_seats.Where(predicate.Compile()).Select(s => s.Id)))
            .ReturnsAsync(true);

        await _sut.ResizeSeatGridAsync(new ResizeSeatGridRequest { RoomId = _room.Id, TotalRows = 2, TotalColumns = 2 });

        deletedIds.Should().ContainSingle();
        deletedIds.Should().Contain(_seats.Single(s => s.RowName == "B" && s.ColIndex == 3).Id);
    }
}
