using Cinema.Business.DTO.Catalog;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class SplashBannerServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly SplashBannerManager _sut;

    private static readonly DateTime Start = new(2026, 1, 1);
    private static readonly DateTime End = new(2026, 12, 31);

    public SplashBannerServiceTests()
    {
        _sut = new SplashBannerManager(_uowMock.Object, new FixedClock(new DateTime(2026, 6, 1)));

        _uowMock.Setup(u => u.SplashBannerStore.CreateAsync(It.IsAny<SplashBanner>()))
            .ReturnsAsync((SplashBanner b) => b);
    }

    private static CreateSplashBannerRequest BaseRequest() => new()
    {
        Title = "Grand Opening",
        ImageUrl = "https://cdn.example.com/banner.png",
        StartDate = Start,
        EndDate = End,
        IsPublished = true,
    };

    [Fact]
    public async Task CreateAsync_EndBeforeStart_Rejected()
    {
        var request = BaseRequest();
        request.StartDate = End;
        request.EndDate = Start;

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_BlankImageUrl_Rejected()
    {
        var request = BaseRequest();
        request.ImageUrl = "  ";

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.com")]
    [InlineData("ftp://example.com/x")]
    [InlineData(@"/\evil.com")]
    public async Task CreateAsync_DisallowedLink_Rejected(string linkUrl)
    {
        var request = BaseRequest();
        request.LinkUrl = linkUrl;

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("/promotions")]
    [InlineData(null)]
    public async Task CreateAsync_AllowedLink_Accepted(string? linkUrl)
    {
        var request = BaseRequest();
        request.LinkUrl = linkUrl;

        var result = await _sut.CreateAsync(request);

        result.LinkUrl.Should().Be(linkUrl);
    }

    [Fact]
    public async Task CreateAsync_TrimsTitleAndStripsTimeOfDay()
    {
        var request = BaseRequest();
        request.Title = "  Grand Opening  ";
        request.StartDate = new DateTime(2026, 1, 1, 13, 45, 0);
        request.EndDate = new DateTime(2026, 12, 31, 23, 0, 0);

        var result = await _sut.CreateAsync(request);

        result.Title.Should().Be("Grand Opening");
        result.StartDate.Should().Be(new DateTime(2026, 1, 1));
        result.EndDate.Should().Be(new DateTime(2026, 12, 31));
    }

    [Fact]
    public async Task GetActiveAsync_UsesLocalDate_ReturnsEmptyWhenNoneActive()
    {
        _uowMock.Setup(u => u.SplashBannerStore.GetActiveAsync(new DateTime(2026, 6, 1)))
            .ReturnsAsync((SplashBanner?)null);

        var result = await _sut.GetActiveAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsSingleItemWhenOneIsActive()
    {
        var banner = new SplashBanner { Id = Guid.NewGuid(), Title = "Active", ImageUrl = "img.png", StartDate = Start, EndDate = End, IsPublished = true };
        _uowMock.Setup(u => u.SplashBannerStore.GetActiveAsync(new DateTime(2026, 6, 1)))
            .ReturnsAsync(banner);

        var result = await _sut.GetActiveAsync();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(banner.Id);
    }

    [Fact]
    public async Task UpdateAsync_MissingId_ThrowsKeyNotFound()
    {
        _uowMock.Setup(u => u.SplashBannerStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((SplashBanner?)null);
        var request = new UpdateSplashBannerRequest { Id = Guid.NewGuid(), Title = "x", ImageUrl = "img.png", StartDate = Start, EndDate = End };

        var act = () => _sut.UpdateAsync(request);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
