using System.Linq.Expressions;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Operations;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Managers;
using Cinema.Business.Security;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class OperationsBoardAndIncidentTests
{
    private const string _pin = "4321";

    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly List<AuditLog> _staged = new();
    private readonly List<Incident> _createdIncidents = new();
    private readonly Guid _theaterId = Guid.NewGuid();
    private readonly Room _room;
    private readonly User _staff;
    private readonly User _manager;
    private readonly IncidentManager _incidents;

    public OperationsBoardAndIncidentTests()
    {
        _uowMock.Setup(u => u.AuditLogStore.Stage(It.IsAny<AuditLog>())).Callback<AuditLog>(row => _staged.Add(row));
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(It.IsAny<User>())).ReturnsAsync((User user) => user);
        _uowMock.Setup(u => u.UserStore.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>())).ReturnsAsync(new Dictionary<Guid, string>());
        _uowMock.Setup(u => u.SeatStore.UpdateAsync(It.IsAny<Seat>())).ReturnsAsync((Seat seat) => seat);
        _uowMock.Setup(u => u.RoomStore.UpdateAsync(It.IsAny<Room>())).ReturnsAsync((Room room) => room);
        _uowMock.Setup(u => u.IncidentStore.UpdateAsync(It.IsAny<Incident>())).ReturnsAsync((Incident incident) => incident);
        _uowMock.Setup(u => u.IncidentStore.CreateAsync(It.IsAny<Incident>()))
            .Callback<Incident>(incident => _createdIncidents.Add(incident))
            .ReturnsAsync((Incident incident) => incident);
        _uowMock.Setup(u => u.IncidentStore.GetUpcomingTicketsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<AffectedTicketRow>());

        _room = new Room { Id = Guid.NewGuid(), Name = "Room 1", TheaterId = _theaterId, Status = RoomStatus.Active };
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(_room.Id)).ReturnsAsync(_room);

        _staff = NewUser(RoleNames.TheaterStaff);
        _manager = NewUser(RoleNames.Admin);
        PasswordHasher.CreateHash(_pin, out var hash, out var salt);
        _manager.OverridePinHash = hash;
        _manager.OverridePinSalt = salt;

        var audit = new AuditLogger(_uowMock.Object);
        _incidents = new IncidentManager(_uowMock.Object, audit, new ManagerOverrideService(_uowMock.Object, audit));
    }

    private User NewUser(string role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = role,
            Status = UserStatus.Active,
            TheaterId = _theaterId,
            UserType = new UserType { Id = Guid.NewGuid(), Name = role }
        };
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(user.Id)).ReturnsAsync(user);
        return user;
    }

    private Seat GivenSeat(Guid? groupId = null, string row = "A", int col = 1)
    {
        var seat = new Seat { Id = Guid.NewGuid(), RoomId = _room.Id, RowName = row, ColIndex = col, SeatGroupId = groupId };
        _uowMock.Setup(u => u.SeatStore.GetByIdAsync(seat.Id)).ReturnsAsync(seat);
        return seat;
    }

    // ── Schedule board ───────────────────────────────────────────────────────

    [Fact]
    public async Task Board_ShowsBufferGap_SoldAndCapacity_WithOneGroupedSoldQuery()
    {
        var showTimeId = Guid.NewGuid();
        var start = new DateTime(2026, 10, 4, 18, 0, 0);
        var end = start.AddMinutes(120);
        _uowMock.Setup(u => u.ScheduleBoardStore.GetRoomsAsync(_theaterId)).ReturnsAsync(new List<BoardRoomRow>
        {
            new(_room.Id, "Room 1", "IMAX", RoomStatus.Active, 20, 100),
            new(Guid.NewGuid(), "Room 2", "Standard", RoomStatus.Maintenance, 10, 80)
        });
        _uowMock.Setup(u => u.ScheduleBoardStore.GetShowTimesAsync(_theaterId, start.Date, start.Date.AddDays(1)))
            .ReturnsAsync(new List<BoardShowTimeRow> { new(showTimeId, _room.Id, Guid.NewGuid(), "Dune", start, end) });
        _uowMock.Setup(u => u.ScheduleBoardStore.GetSoldCountsAsync(_theaterId, start.Date, start.Date.AddDays(1)))
            .ReturnsAsync(new Dictionary<(Guid ShowTimeId, Guid RoomId), int> { [(showTimeId, _room.Id)] = 37 });

        var board = await new ScheduleBoardManager(_uowMock.Object).GetScheduleBoardAsync(_theaterId, start);

        board.Rooms.Should().HaveCount(2);
        var show = board.Rooms[0].ShowTimes.Should().ContainSingle().Subject;
        show.Start.Should().Be(start);
        show.End.Should().Be(end);
        show.BufferEnd.Should().Be(end.AddMinutes(20));
        show.MovieTitle.Should().Be("Dune");
        show.Sold.Should().Be(37);
        show.Capacity.Should().Be(100);
        board.Rooms[1].ShowTimes.Should().BeEmpty();
        board.Rooms[1].RoomStatus.Should().Be(RoomStatus.Maintenance);
        _uowMock.Verify(u => u.ScheduleBoardStore.GetSoldCountsAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    // ── Blocking a seat ──────────────────────────────────────────────────────

    [Fact]
    public async Task BlockSeat_ByApprover_DeactivatesSeat_ReturnsAffectedTickets_CancelsNothing_AndAudits()
    {
        var seat = GivenSeat();
        var ticket = new AffectedTicketRow(Guid.NewGuid(), "INV-1", "Lan", "0900000000", Guid.NewGuid(), _room.Id, "Room 1", "Dune", DateTime.Now.AddHours(3), seat.Id, "A", 1);
        _uowMock.Setup(u => u.IncidentStore.GetUpcomingTicketsAsync(_room.Id, It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(seat.Id)), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<AffectedTicketRow> { ticket });

        var result = await _incidents.BlockSeatAsync(_theaterId, _manager.Id, new BlockSeatRequest { SeatId = seat.Id });

        seat.IsActive.Should().BeFalse();
        result.BlockedSeatIds.Should().BeEquivalentTo(new[] { seat.Id });
        result.AffectedTickets.Should().ContainSingle().Which.InvoiceCode.Should().Be("INV-1");
        _createdIncidents.Should().ContainSingle().Which.BlocksSeat.Should().BeTrue();
        result.IncidentId.Should().Be(_createdIncidents[0].Id);
        _staged.Should().ContainSingle();
        _staged[0].Action.Should().Be(AuditAction.BlockSeat);
        _staged[0].ActorUserId.Should().Be(_manager.Id);
        _staged[0].ApproverUserId.Should().Be(_manager.Id);
        _staged[0].EntityId.Should().Be(seat.Id);
        // Nothing is cancelled: the invoice store is never touched.
        _uowMock.VerifyGet(u => u.InvoiceStore, Times.Never);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task BlockSeat_ByStaffWithoutOverride_Is403AndChangesNothing()
    {
        var seat = GivenSeat();

        var act = () => _incidents.BlockSeatAsync(_theaterId, _staff.Id, new BlockSeatRequest { SeatId = seat.Id });

        await act.Should().ThrowAsync<AccessDeniedException>();
        seat.IsActive.Should().BeTrue();
        _createdIncidents.Should().BeEmpty();
        _uowMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task BlockSeat_ByStaffWithManagerOverride_Works_AndAuditsBothActorAndApprover()
    {
        var seat = GivenSeat();

        await _incidents.BlockSeatAsync(_theaterId, _staff.Id, new BlockSeatRequest
        {
            SeatId = seat.Id,
            Override = new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = _pin }
        });

        seat.IsActive.Should().BeFalse();
        var audit = _staged.Should().ContainSingle(a => a.Action == AuditAction.BlockSeat).Subject;
        audit.ActorUserId.Should().Be(_staff.Id);
        audit.ApproverUserId.Should().Be(_manager.Id);
    }

    [Fact]
    public async Task BlockSeat_WrongPin_Is403AndSeatStaysOnSale()
    {
        var seat = GivenSeat();

        var act = () => _incidents.BlockSeatAsync(_theaterId, _staff.Id, new BlockSeatRequest
        {
            SeatId = seat.Id,
            Override = new ManagerOverrideDTO { ApproverUserId = _manager.Id, Pin = "0000" }
        });

        await act.Should().ThrowAsync<AccessDeniedException>();
        seat.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task BlockSeat_DoubleSeat_BlocksBothHalves()
    {
        var groupId = Guid.NewGuid();
        var left = GivenSeat(groupId, "B", 1);
        var right = GivenSeat(groupId, "B", 2);
        _uowMock.Setup(u => u.SeatStore.FindAllAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .ReturnsAsync(new List<Seat> { left, right });

        var result = await _incidents.BlockSeatAsync(_theaterId, _manager.Id, new BlockSeatRequest { SeatId = left.Id });

        left.IsActive.Should().BeFalse();
        right.IsActive.Should().BeFalse();
        result.BlockedSeatIds.Should().BeEquivalentTo(new[] { left.Id, right.Id });
    }

    [Fact]
    public async Task BlockSeat_SeatOfAnotherTheater_Is403()
    {
        var seat = GivenSeat();

        var act = () => _incidents.BlockSeatAsync(Guid.NewGuid(), _manager.Id, new BlockSeatRequest { SeatId = seat.Id });

        await act.Should().ThrowAsync<AccessDeniedException>();
        seat.IsActive.Should().BeTrue();
    }

    // ── Blocking a room ──────────────────────────────────────────────────────

    [Fact]
    public async Task BlockRoom_ByApprover_SetsMaintenance_ReturnsTickets_AndAudits()
    {
        var ticket = new AffectedTicketRow(Guid.NewGuid(), "INV-2", "Minh", "0911111111", Guid.NewGuid(), _room.Id, "Room 1", "Dune", DateTime.Now.AddHours(2), Guid.NewGuid(), "C", 5);
        _uowMock.Setup(u => u.IncidentStore.GetUpcomingTicketsAsync(_room.Id, null, It.IsAny<DateTime>()))
            .ReturnsAsync(new List<AffectedTicketRow> { ticket });

        var result = await _incidents.BlockRoomAsync(_theaterId, _manager.Id, new BlockRoomRequest { RoomId = _room.Id, Title = "Projector down" });

        _room.Status.Should().Be(RoomStatus.Maintenance);
        result.BlockedRoomId.Should().Be(_room.Id);
        result.AffectedTickets.Should().ContainSingle();
        _createdIncidents.Should().ContainSingle().Which.Title.Should().Be("Projector down");
        _staged.Should().ContainSingle(a => a.Action == AuditAction.BlockRoom && a.ApproverUserId == _manager.Id);
        _uowMock.VerifyGet(u => u.InvoiceStore, Times.Never);
    }

    [Fact]
    public async Task BlockRoom_ByStaffWithoutOverride_Is403()
    {
        var act = () => _incidents.BlockRoomAsync(_theaterId, _staff.Id, new BlockRoomRequest { RoomId = _room.Id });

        await act.Should().ThrowAsync<AccessDeniedException>();
        _room.Status.Should().Be(RoomStatus.Active);
    }

    [Fact]
    public async Task BlockRoom_AlreadyInMaintenance_Is400()
    {
        _room.Status = RoomStatus.Maintenance;

        var act = () => _incidents.BlockRoomAsync(_theaterId, _manager.Id, new BlockRoomRequest { RoomId = _room.Id });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Report / resolve ─────────────────────────────────────────────────────

    [Fact]
    public async Task Report_AnyStaff_CreatesOpenIncident_WithoutBlockingAnything()
    {
        var seat = GivenSeat();

        var dto = await _incidents.ReportAsync(_theaterId, _staff.Id, new ReportIncidentRequest
        {
            Category = IncidentCategory.Seat,
            Title = "  Broken recliner ",
            SeatId = seat.Id
        });

        dto.Status.Should().Be(IncidentStatus.Open);
        dto.Title.Should().Be("Broken recliner");
        dto.RoomId.Should().Be(_room.Id);
        dto.SeatLabel.Should().Be("A1");
        dto.BlocksSeat.Should().BeFalse();
        seat.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_WithUnblock_ReactivatesSeat_ByApproverOnly()
    {
        var seat = GivenSeat();
        await _incidents.BlockSeatAsync(_theaterId, _manager.Id, new BlockSeatRequest { SeatId = seat.Id });
        var incident = _createdIncidents[0];
        _uowMock.Setup(u => u.IncidentStore.GetByIdAsync(incident.Id)).ReturnsAsync(incident);
        _staged.Clear();

        var refused = () => _incidents.ResolveAsync(new[] { _theaterId }, _staff.Id, new ResolveIncidentRequest { IncidentId = incident.Id, Unblock = true });
        await refused.Should().ThrowAsync<AccessDeniedException>();
        seat.IsActive.Should().BeFalse();

        var dto = await _incidents.ResolveAsync(new[] { _theaterId }, _manager.Id, new ResolveIncidentRequest { IncidentId = incident.Id, Unblock = true, ResolutionNote = "fixed" });

        seat.IsActive.Should().BeTrue();
        dto.Status.Should().Be(IncidentStatus.Resolved);
        dto.BlocksSeat.Should().BeFalse();
        _staged.Should().ContainSingle(a => a.Action == AuditAction.BlockSeat);
    }

    [Fact]
    public async Task Resolve_WithoutUnblock_LeavesBlockInPlace_AndTwiceIs400()
    {
        var seat = GivenSeat();
        await _incidents.BlockSeatAsync(_theaterId, _manager.Id, new BlockSeatRequest { SeatId = seat.Id });
        var incident = _createdIncidents[0];
        _uowMock.Setup(u => u.IncidentStore.GetByIdAsync(incident.Id)).ReturnsAsync(incident);

        await _incidents.ResolveAsync(null, _staff.Id, new ResolveIncidentRequest { IncidentId = incident.Id });

        seat.IsActive.Should().BeFalse();
        var again = () => _incidents.ResolveAsync(null, _staff.Id, new ResolveIncidentRequest { IncidentId = incident.Id });
        await again.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Resolve_IncidentOutsideScope_Is403()
    {
        var incident = new Incident { Id = Guid.NewGuid(), TheaterId = _theaterId, Title = "x" };
        _uowMock.Setup(u => u.IncidentStore.GetByIdAsync(incident.Id)).ReturnsAsync(incident);

        var act = () => _incidents.ResolveAsync(new[] { Guid.NewGuid() }, _staff.Id, new ResolveIncidentRequest { IncidentId = incident.Id });

        await act.Should().ThrowAsync<AccessDeniedException>();
    }
}
