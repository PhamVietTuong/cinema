using Cinema.Business.Contracts;
using Cinema.Business.DTO.Theaters;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;
using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.Tests;

public class TheaterManagerTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly TheaterManager _sut;

    public TheaterManagerTests()
    {
        _sut = new TheaterManager(_uowMock.Object, new Mock<ISeatTypeManager>().Object);
    }

    private static UpdateTheaterRequest Update(int seats = 10, int? cap = 50, int? holidayCap = 50)
    {
        return new UpdateTheaterRequest { Id = Guid.NewGuid(), Name = "T", MaxSeatsPerBooking = seats, MaxPromotionDiscountPercent = cap, MaxHolidayPromotionDiscountPercent = holidayCap };
    }

    private void SetupExisting(UpdateTheaterRequest request)
    {
        _uowMock.Setup(u => u.TheaterStore.GetByIdAsync(request.Id)).ReturnsAsync(new Theater { Id = request.Id });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public async Task UpdateAsync_ValidNormalCap_Saves(int cap)
    {
        var request = Update(cap: cap);
        SetupExisting(request);

        var dto = await _sut.UpdateAsync(request);

        dto.MaxPromotionDiscountPercent.Should().Be(cap);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public async Task UpdateAsync_NormalCapOutOfRange_IsRejected(int cap)
    {
        var request = Update(cap: cap);
        SetupExisting(request);

        await FluentActions.Awaiting(() => _sut.UpdateAsync(request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*between 0 and 50*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task UpdateAsync_HolidayCapOutOfRange_IsRejected(int cap)
    {
        var request = Update(holidayCap: cap);
        SetupExisting(request);

        await FluentActions.Awaiting(() => _sut.UpdateAsync(request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*between 0 and 100*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task UpdateAsync_MaxSeatsOutOfRange_IsRejected(int seats)
    {
        var request = Update(seats: seats);
        SetupExisting(request);

        await FluentActions.Awaiting(() => _sut.UpdateAsync(request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*between 1 and 50*");
    }

    [Fact]
    public async Task CreateAsync_NormalCapOutOfRange_IsRejected()
    {
        var request = new CreateTheaterRequest { Name = "T", MaxPromotionDiscountPercent = 51 };

        await FluentActions.Awaiting(() => _sut.CreateAsync(request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Max promotion discount must be between 0 and 50 percent.");
    }

    [Fact]
    public async Task CreateAsync_HolidayCapOutOfRange_IsRejected()
    {
        var request = new CreateTheaterRequest { Name = "T", MaxHolidayPromotionDiscountPercent = 101 };

        await FluentActions.Awaiting(() => _sut.CreateAsync(request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Max holiday promotion discount must be between 0 and 100 percent.");
    }

    [Fact]
    public async Task UpdateAsync_OmittedCaps_KeepTheStoredValues()
    {
        var request = Update(cap: null, holidayCap: null);
        _uowMock.Setup(u => u.TheaterStore.GetByIdAsync(request.Id))
            .ReturnsAsync(new Theater { Id = request.Id, MaxPromotionDiscountPercent = 30, MaxHolidayPromotionDiscountPercent = 80 });

        var dto = await _sut.UpdateAsync(request);

        dto.MaxPromotionDiscountPercent.Should().Be(30);
        dto.MaxHolidayPromotionDiscountPercent.Should().Be(80);
    }

    [Fact]
    public void CreateRequest_DefaultsBothCapsTo50()
    {
        var request = new CreateTheaterRequest();

        request.MaxPromotionDiscountPercent.Should().Be(50);
        request.MaxHolidayPromotionDiscountPercent.Should().Be(50);
    }

    [Theory]
    [InlineData(51, 50, false)]
    [InlineData(50, 100, true)]
    [InlineData(50, 101, false)]
    [InlineData(-1, 50, false)]
    public void UpdateRequest_RangeAttributes_RejectOutOfRange(int cap, int holidayCap, bool valid)
    {
        var request = Update(cap: cap, holidayCap: holidayCap);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true)
            .Should().Be(valid);
    }
}
