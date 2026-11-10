using System.Linq.Expressions;
using Cinema.Business.DTO.Loyalty;
using Cinema.Business.Helpers;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class LoyaltySettingServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uow = new();
    private readonly LoyaltySettingManager _sut;

    public LoyaltySettingServiceTests()
    {
        _sut = new LoyaltySettingManager(_uow.Object);
        // Default: no rows exist unless a test sets one up.
        _uow.Setup(u => u.LoyaltySettingStore.GetOverrideAsync(It.IsAny<Guid>())).ReturnsAsync((LoyaltySetting?)null);
        // Default: the theater named in a per-theater save exists, unless a test says otherwise.
        _uow.Setup(u => u.TheaterStore.ExistsAsync(It.IsAny<Expression<Func<Theater, bool>>>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task GetEffectiveAsync_NoRow_ReturnsBuiltInDefaults()
    {
        var rate = await _sut.GetEffectiveAsync(Guid.NewGuid());

        rate.PointsPerUnit.Should().Be(1000);
        rate.PointValueVnd.Should().Be(10);
        rate.IsCustom.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_NoExistingRow_CreatesOne()
    {
        LoyaltySetting? created = null;
        _uow.Setup(u => u.LoyaltySettingStore.CreateAsync(It.IsAny<LoyaltySetting>()))
            .ReturnsAsync((LoyaltySetting s) => { created = s; return s; });

        var dto = await _sut.SaveAsync(new SaveLoyaltySettingRequest { TheaterId = Guid.NewGuid(), PointsPerUnit = 1500, PointValueVnd = 1500 });

        created.Should().NotBeNull();
        dto.PointsPerUnit.Should().Be(1500);
        dto.PointValueVnd.Should().Be(1500);
    }

    [Fact]
    public async Task SaveAsync_ExistingTheaterOverride_UpdatesIt()
    {
        var theaterId = Guid.NewGuid();
        var existing = new LoyaltySetting { TheaterId = theaterId, PointsPerUnit = 1000, PointValueVnd = 1000 };
        _uow.Setup(u => u.LoyaltySettingStore.GetOverrideAsync(theaterId)).ReturnsAsync(existing);
        _uow.Setup(u => u.LoyaltySettingStore.UpdateAsync(existing)).ReturnsAsync(existing);

        var dto = await _sut.SaveAsync(new SaveLoyaltySettingRequest { TheaterId = theaterId, PointsPerUnit = 2000, PointValueVnd = 2500 });

        existing.PointsPerUnit.Should().Be(2000);
        existing.PointValueVnd.Should().Be(2500);
        dto.TheaterId.Should().Be(theaterId);
    }

    [Fact]
    public async Task SaveAsync_UnknownTheater_ThrowsNotFound()
    {
        var theaterId = Guid.NewGuid();
        _uow.Setup(u => u.TheaterStore.ExistsAsync(It.IsAny<Expression<Func<Theater, bool>>>())).ReturnsAsync(false);

        await FluentActions.Awaiting(() => _sut.SaveAsync(new SaveLoyaltySettingRequest { TheaterId = theaterId, PointsPerUnit = 1000, PointValueVnd = 1000 }))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(1000, 0)]
    [InlineData(-1, 1000)]
    public async Task SaveAsync_NonPositiveValue_Throws(int pointsPerUnit, int pointValueVnd)
    {
        await FluentActions.Awaiting(() => _sut.SaveAsync(new SaveLoyaltySettingRequest { TheaterId = Guid.NewGuid(), PointsPerUnit = pointsPerUnit, PointValueVnd = pointValueVnd }))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResetToDefaultAsync_NoRow_ReturnsFalse()
    {
        var result = await _sut.ResetToDefaultAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ResetToDefaultAsync_ExistingRow_DeletesAndReturnsTrue()
    {
        var theaterId = Guid.NewGuid();
        var existing = new LoyaltySetting { TheaterId = theaterId, PointsPerUnit = 2000, PointValueVnd = 2000 };
        _uow.Setup(u => u.LoyaltySettingStore.GetOverrideAsync(theaterId)).ReturnsAsync(existing);
        _uow.Setup(u => u.LoyaltySettingStore.DeleteAsync(existing)).ReturnsAsync(existing);

        var result = await _sut.ResetToDefaultAsync(theaterId);

        result.Should().BeTrue();
        _uow.Verify(u => u.LoyaltySettingStore.DeleteAsync(existing), Times.Once);
    }

    [Fact]
    public async Task GetEffectiveAsync_TheaterWithOverride_UsesOverride()
    {
        var theaterId = Guid.NewGuid();
        _uow.Setup(u => u.LoyaltySettingStore.GetOverrideAsync(theaterId))
            .ReturnsAsync(new LoyaltySetting { TheaterId = theaterId, PointsPerUnit = 500, PointValueVnd = 2000 });

        var rate = await _sut.GetEffectiveAsync(theaterId);

        rate.PointsPerUnit.Should().Be(500);
        rate.PointValueVnd.Should().Be(2000);
        rate.IsCustom.Should().BeTrue();
    }

    [Fact]
    public async Task LoyaltyRates_LoadAsync_NoRowsAtAll_FallsBackToBuiltInDefaults()
    {
        var (pointsPerUnit, pointValueVnd) = await LoyaltyRates.LoadAsync(_uow.Object, Guid.NewGuid());

        pointsPerUnit.Should().Be(LoyaltyRates.DefaultPointsPerUnit);
        pointValueVnd.Should().Be(LoyaltyRates.DefaultPointValueVnd);
    }
}
