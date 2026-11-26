using System.Linq.Expressions;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class HolidayTimeSlotPriceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly HolidayManager _sut;

    private static readonly Guid TheaterId = Guid.NewGuid();
    private static readonly Guid HolidayId = Guid.NewGuid();
    private static readonly Guid SlotA     = Guid.NewGuid();
    private static readonly Guid SlotB     = Guid.NewGuid();

    public HolidayTimeSlotPriceTests()
    {
        _sut = new HolidayManager(_uowMock.Object);

        _uowMock.Setup(u => u.HolidayStore.ExistsAsync(It.IsAny<Expression<Func<Holiday, bool>>>()))
            .ReturnsAsync(true);
        // Both listed slots belong to the theater unless a test says otherwise.
        _uowMock.Setup(u => u.TimeSlotStore.CountAsync(It.IsAny<Expression<Func<TimeSlot, bool>>>()))
            .ReturnsAsync(2);
        _uowMock.Setup(u => u.HolidayTimeSlotPriceStore.UpdateRangeAsync(It.IsAny<IEnumerable<HolidayTimeSlotPrice>>()))
            .ReturnsAsync((IEnumerable<HolidayTimeSlotPrice> e) => e);
        _uowMock.Setup(u => u.HolidayTimeSlotPriceStore.CreateRangeAsync(It.IsAny<List<HolidayTimeSlotPrice>>()))
            .ReturnsAsync((List<HolidayTimeSlotPrice> e) => e);
    }

    private static UpsertHolidayTimeSlotPricesRequest Request(params HolidayTimeSlotPriceItem[] items)
    {
        return new UpsertHolidayTimeSlotPricesRequest { HolidayId = HolidayId, TheaterId = TheaterId, Items = items.ToList() };
    }

    [Fact]
    public async Task Upsert_CreatesNewSlot_UpdatesExistingSlot()
    {
        var existing = new HolidayTimeSlotPrice { HolidayId = HolidayId, TimeSlotId = SlotA, PriceMultiplier = 1.2 };
        _uowMock.Setup(u => u.HolidayTimeSlotPriceStore.FindAllAsync(It.IsAny<Expression<Func<HolidayTimeSlotPrice, bool>>>()))
            .ReturnsAsync(new List<HolidayTimeSlotPrice> { existing });

        var result = await _sut.UpsertTimeSlotPricesAsync(Request(
            new HolidayTimeSlotPriceItem { TimeSlotId = SlotA, PriceMultiplier = 1.8 },
            new HolidayTimeSlotPriceItem { TimeSlotId = SlotB, PriceMultiplier = 0.9 }));

        result.Should().HaveCount(2);
        existing.PriceMultiplier.Should().Be(1.8);
        _uowMock.Verify(u => u.HolidayTimeSlotPriceStore.CreateRangeAsync(
            It.Is<List<HolidayTimeSlotPrice>>(e => e.Count == 1 && e[0].TimeSlotId == SlotB && e[0].PriceMultiplier == 0.9)), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task Upsert_NullMultiplier_RemovesExistingOverride()
    {
        var existing = new HolidayTimeSlotPrice { HolidayId = HolidayId, TimeSlotId = SlotA, PriceMultiplier = 1.2 };
        _uowMock.Setup(u => u.HolidayTimeSlotPriceStore.FindAllAsync(It.IsAny<Expression<Func<HolidayTimeSlotPrice, bool>>>()))
            .ReturnsAsync(new List<HolidayTimeSlotPrice> { existing });
        _uowMock.Setup(u => u.HolidayTimeSlotPriceStore.DeleteAsync(existing)).ReturnsAsync(existing);

        var result = await _sut.UpsertTimeSlotPricesAsync(Request(
            new HolidayTimeSlotPriceItem { TimeSlotId = SlotA, PriceMultiplier = null },
            new HolidayTimeSlotPriceItem { TimeSlotId = SlotB, PriceMultiplier = null }));

        result.Should().BeEmpty();
        _uowMock.Verify(u => u.HolidayTimeSlotPriceStore.DeleteAsync(existing), Times.Once);
        _uowMock.Verify(u => u.HolidayTimeSlotPriceStore.CreateRangeAsync(It.IsAny<List<HolidayTimeSlotPrice>>()), Times.Never);
    }

    [Fact]
    public async Task Upsert_SlotOfAnotherTheater_Throws()
    {
        // Only one of the two listed slots belongs to the theater.
        _uowMock.Setup(u => u.TimeSlotStore.CountAsync(It.IsAny<Expression<Func<TimeSlot, bool>>>()))
            .ReturnsAsync(1);

        var act = () => _sut.UpsertTimeSlotPricesAsync(Request(
            new HolidayTimeSlotPriceItem { TimeSlotId = SlotA, PriceMultiplier = 1.5 },
            new HolidayTimeSlotPriceItem { TimeSlotId = SlotB, PriceMultiplier = 1.5 }));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not belong to this theater*");
        _uowMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    public async Task Upsert_NonPositiveMultiplier_Throws(double multiplier)
    {
        var act = () => _sut.UpsertTimeSlotPricesAsync(Request(
            new HolidayTimeSlotPriceItem { TimeSlotId = SlotA, PriceMultiplier = multiplier }));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*greater than 0*");
    }

    [Fact]
    public async Task Upsert_DuplicateSlot_Throws()
    {
        var act = () => _sut.UpsertTimeSlotPricesAsync(Request(
            new HolidayTimeSlotPriceItem { TimeSlotId = SlotA, PriceMultiplier = 1.5 },
            new HolidayTimeSlotPriceItem { TimeSlotId = SlotA, PriceMultiplier = 1.7 }));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*only be listed once*");
    }

    [Fact]
    public async Task Upsert_UnknownHoliday_ThrowsNotFound()
    {
        _uowMock.Setup(u => u.HolidayStore.ExistsAsync(It.IsAny<Expression<Func<Holiday, bool>>>()))
            .ReturnsAsync(false);

        var act = () => _sut.UpsertTimeSlotPricesAsync(Request(
            new HolidayTimeSlotPriceItem { TimeSlotId = SlotA, PriceMultiplier = 1.5 }));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
