using Cinema.Business.Contracts;
using Cinema.Service.WebApiHost.Hubs;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace Cinema.Service.WebApiHost.Tests;

/// <summary>The hub contract for seat locks: what is refused, and what is broadcast to the room.</summary>
public class BookingHubTests
{
    private const string Connection = "conn-1";
    private readonly Guid _showTime = Guid.NewGuid();
    private readonly Guid _room = Guid.NewGuid();
    private readonly Guid _seat = Guid.NewGuid();

    private readonly Mock<IBookingManager> _manager = new();
    private readonly Mock<IClientProxy> _group = new();
    private readonly Mock<ISingleClientProxy> _caller = new();
    private readonly Mock<IGroupManager> _groups = new();

    private BookingHub CreateHub()
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns(Connection);
        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.Caller).Returns(_caller.Object);
        clients.Setup(c => c.Group(BookingHub.RoomGroup(_showTime, _room))).Returns(_group.Object);
        _groups.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return new BookingHub(_manager.Object)
        {
            Context = context.Object,
            Clients = clients.Object,
            Groups = _groups.Object,
        };
    }

    private static void VerifySent<T>(Mock<T> proxy, string method, Times times, params object[] expectedArgs) where T : class, IClientProxy
    {
        proxy.Verify(p => p.SendCoreAsync(method, It.Is<object?[]>(a => a.SequenceEqual(expectedArgs)), It.IsAny<CancellationToken>()), times);
    }

    [Trait("Req", "BR-SEAT-01")]
    [Fact]
    public async Task LockSeat_WhenAnotherConnectionHoldsIt_IsRefusedToTheCallerOnly()
    {
        _manager.Setup(m => m.IsSeatLocked(_showTime, _room, _seat, Connection)).Returns(true);

        await CreateHub().LockSeat(_showTime, _room, _seat);

        VerifySent(_caller, "SeatLockFailed", Times.Once(), _seat, "Seat is already locked.");
        VerifySent(_group, "SeatLocked", Times.Never(), _seat, Connection);
        _manager.Verify(m => m.LockSeat(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Trait("Req", "BR-SEAT-01")]
    [Fact]
    public async Task LockSeat_WhenFree_LocksAndBroadcastsToTheRoom()
    {
        _manager.Setup(m => m.IsSeatLocked(_showTime, _room, _seat, Connection)).Returns(false);

        await CreateHub().LockSeat(_showTime, _room, _seat);

        _manager.Verify(m => m.LockSeat(_showTime, _room, _seat, Connection), Times.Once);
        VerifySent(_group, "SeatLocked", Times.Once(), _seat, Connection);
        VerifySent(_caller, "SeatLockFailed", Times.Never(), _seat, "Seat is already locked.");
    }

    [Trait("Req", "BR-SEAT-01")]
    [Fact]
    public async Task UnlockSeat_ByANonOwner_BroadcastsNothing()
    {
        _manager.Setup(m => m.UnlockSeat(_showTime, _room, _seat, Connection)).Returns(false);

        await CreateHub().UnlockSeat(_showTime, _room, _seat);

        VerifySent(_group, "SeatUnlocked", Times.Never(), _seat);
    }

    [Trait("Req", "BR-SEAT-01")]
    [Fact]
    public async Task UnlockSeat_ByTheOwner_BroadcastsTheRelease()
    {
        _manager.Setup(m => m.UnlockSeat(_showTime, _room, _seat, Connection)).Returns(true);

        await CreateHub().UnlockSeat(_showTime, _room, _seat);

        VerifySent(_group, "SeatUnlocked", Times.Once(), _seat);
    }

    [Trait("Req", "BR-SEAT-01")]
    [Fact]
    public async Task Disconnect_BroadcastsEachHeldSeatToItsOwnRoom()
    {
        var otherRoom = Guid.NewGuid();
        var otherSeat = Guid.NewGuid();
        var otherGroup = new Mock<IClientProxy>();
        _manager.Setup(m => m.ReleaseConnectionLocks(Connection)).Returns(new List<(Guid, Guid, Guid)>
        {
            (_showTime, _room, _seat),
            (_showTime, otherRoom, otherSeat),
        });
        var hub = CreateHub();
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(c => c.Group(BookingHub.RoomGroup(_showTime, _room))).Returns(_group.Object);
        clients.Setup(c => c.Group(BookingHub.RoomGroup(_showTime, otherRoom))).Returns(otherGroup.Object);
        hub.Clients = clients.Object;

        await hub.OnDisconnectedAsync(null);

        VerifySent(_group, "SeatUnlocked", Times.Once(), _seat);
        VerifySent(_group, "SeatUnlocked", Times.Never(), otherSeat);
        VerifySent(otherGroup, "SeatUnlocked", Times.Once(), otherSeat);
        VerifySent(otherGroup, "SeatUnlocked", Times.Never(), _seat);
    }
}
