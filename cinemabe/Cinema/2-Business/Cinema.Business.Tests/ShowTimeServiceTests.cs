using Cinema.Business.DTO.Catalog;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

/// <summary>
/// Covers the room-class ↔ projection-format guard. The two are independent axes — a room class
/// sets the base price, the screening's dimension is chosen per showtime — so nothing but this
/// check stops a 3D screening from being scheduled into a room with no 3D projector.
/// </summary>
public class ShowTimeServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly ShowTimeManager _sut;

    private static readonly Guid MovieId    = Guid.NewGuid();
    private static readonly Guid RoomId     = Guid.NewGuid();
    private static readonly Guid RoomTypeId = Guid.NewGuid();

    public ShowTimeServiceTests()
    {
        _sut = new ShowTimeManager(_uowMock.Object);
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(RoomId))
            .ReturnsAsync(new Room { Id = RoomId, RoomTypeId = RoomTypeId });
        _uowMock.Setup(u => u.ShowTimeStore.HasRoomOverlapAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>()))
            .ReturnsAsync(false);
    }

    private void GivenRoomClass(string name, bool supportsThreeD)
    {
        _uowMock.Setup(u => u.RoomTypeStore.GetByIdAsync(RoomTypeId))
            .ReturnsAsync(new RoomType { Id = RoomTypeId, Name = name, SupportsThreeD = supportsThreeD });
    }

    private static CreateShowTimeRequest Request(ProjectionForm form)
    {
        return new CreateShowTimeRequest
        {
            MovieId        = MovieId,
            StartTime      = DateTime.Now.AddDays(1),
            EndTime        = DateTime.Now.AddDays(1).AddHours(2),
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
        // A 2D showtime never needs the room class, so it must not pay for the lookup.
        _uowMock.Verify(u => u.RoomTypeStore.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_AllowsThreeD_WhenRoomClassSupportsIt()
    {
        GivenRoomClass("IMAX", supportsThreeD: true);
        _uowMock.Setup(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>())).ReturnsAsync((ShowTime s) => s);

        await _sut.CreateAsync(Request(ProjectionForm.ThreeD));

        _uowMock.Verify(u => u.ShowTimeStore.CreateAsync(It.IsAny<ShowTime>()), Times.Once);
    }
}
