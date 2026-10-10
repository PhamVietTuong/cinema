using System.Linq.Expressions;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cinema.Business.Tests;

public class TicketPriceManagerTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly TicketPriceManager _sut;

    private static readonly Guid TheaterId  = Guid.NewGuid();
    private static readonly Guid RoomTypeId = Guid.NewGuid();
    private static readonly Guid TimeSlotId = Guid.NewGuid();

    public TicketPriceManagerTests()
    {
        _sut = new TicketPriceManager(_uowMock.Object);

        _uowMock.Setup(u => u.RoomTypeStore.ExistsAsync(It.IsAny<Expression<Func<RoomType, bool>>>()))
            .ReturnsAsync(true);
        _uowMock.Setup(u => u.TimeSlotStore.ExistsAsync(It.IsAny<Expression<Func<TimeSlot, bool>>>()))
            .ReturnsAsync(true);
        _uowMock.Setup(u => u.TicketPriceStore.ExistsAsync(It.IsAny<Expression<Func<TicketPrice, bool>>>()))
            .ReturnsAsync(false);
    }

    [Fact]
    public async Task Upsert_CreatesMissingDays_UpdatesExisting()
    {
        var existingRow = new TicketPrice
        {
            TheaterId = TheaterId, RoomTypeId = RoomTypeId, TimeSlotId = TimeSlotId,
            DayOfWeek = DayOfWeek.Friday, PriceMultiplier = 1.0,
        };
        _uowMock.Setup(u => u.TicketPriceStore.FindAllAsync(It.IsAny<Expression<Func<TicketPrice, bool>>>()))
            .ReturnsAsync(new List<TicketPrice> { existingRow });
        _uowMock.Setup(u => u.TicketPriceStore.UpdateRangeAsync(It.IsAny<IEnumerable<TicketPrice>>()))
            .ReturnsAsync((IEnumerable<TicketPrice> e) => e);
        _uowMock.Setup(u => u.TicketPriceStore.CreateRangeAsync(It.IsAny<List<TicketPrice>>()))
            .ReturnsAsync((List<TicketPrice> e) => e);

        var result = await _sut.UpsertAsync(new UpsertTicketPricesRequest
        {
            TheaterId = TheaterId, RoomTypeId = RoomTypeId, TimeSlotId = TimeSlotId,
            DaysOfWeek = new List<DayOfWeek> { DayOfWeek.Friday, DayOfWeek.Saturday },
            PriceMultiplier = 1.3,
        });

        result.Should().HaveCount(2);
        existingRow.PriceMultiplier.Should().Be(1.3);
        _uowMock.Verify(u => u.TicketPriceStore.UpdateRangeAsync(It.Is<IEnumerable<TicketPrice>>(e => e.Count() == 1)), Times.Once);
        _uowMock.Verify(u => u.TicketPriceStore.CreateRangeAsync(It.Is<List<TicketPrice>>(e => e.Count == 1 && e[0].DayOfWeek == DayOfWeek.Saturday)), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task Upsert_EmptyDays_Throws()
    {
        var act = () => _sut.UpsertAsync(new UpsertTicketPricesRequest
        {
            TheaterId = TheaterId, RoomTypeId = RoomTypeId, TimeSlotId = TimeSlotId,
            DaysOfWeek = new List<DayOfWeek>(),
            PriceMultiplier = 1.2,
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Upsert_NonPositiveMultiplier_Throws()
    {
        var act = () => _sut.UpsertAsync(new UpsertTicketPricesRequest
        {
            TheaterId = TheaterId, RoomTypeId = RoomTypeId, TimeSlotId = TimeSlotId,
            DaysOfWeek = new List<DayOfWeek> { DayOfWeek.Monday },
            PriceMultiplier = 0,
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Upsert_TimeSlotOfOtherTheater_Throws()
    {
        _uowMock.Setup(u => u.TimeSlotStore.ExistsAsync(It.IsAny<Expression<Func<TimeSlot, bool>>>()))
            .ReturnsAsync(false);

        var act = () => _sut.UpsertAsync(new UpsertTicketPricesRequest
        {
            TheaterId = TheaterId, RoomTypeId = RoomTypeId, TimeSlotId = TimeSlotId,
            DaysOfWeek = new List<DayOfWeek> { DayOfWeek.Monday },
            PriceMultiplier = 1.2,
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Upsert_ConcurrentDuplicateAtCommit_ThrowsTranslatedMessage()
    {
        // A second request inserted one of these days between our existence check and this commit —
        // the unique index catches it as a DbUpdateException, not our own pre-check.
        _uowMock.Setup(u => u.TicketPriceStore.FindAllAsync(It.IsAny<Expression<Func<TicketPrice, bool>>>()))
            .ReturnsAsync(new List<TicketPrice>());
        _uowMock.Setup(u => u.TicketPriceStore.CreateRangeAsync(It.IsAny<List<TicketPrice>>()))
            .ThrowsAsync(new DbUpdateException("Violation of UNIQUE KEY constraint"));

        var act = () => _sut.UpsertAsync(new UpsertTicketPricesRequest
        {
            TheaterId = TheaterId, RoomTypeId = RoomTypeId, TimeSlotId = TimeSlotId,
            DaysOfWeek = new List<DayOfWeek> { DayOfWeek.Monday },
            PriceMultiplier = 1.2,
        });

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("A ticket price already exists for this room type, time slot and day.");
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_Duplicate_Throws()
    {
        _uowMock.Setup(u => u.TicketPriceStore.ExistsAsync(It.IsAny<Expression<Func<TicketPrice, bool>>>()))
            .ReturnsAsync(true);

        var act = () => _sut.CreateAsync(new CreateTicketPriceRequest
        {
            TheaterId = TheaterId, RoomTypeId = RoomTypeId, TimeSlotId = TimeSlotId,
            DayOfWeek = DayOfWeek.Monday, PriceMultiplier = 1.2,
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
