using Cinema.Business.DTO.Invoices;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class GiftCardServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uow = new();
    private readonly GiftCardManager _sut;

    public GiftCardServiceTests()
    {
        _sut = new GiftCardManager(_uow.Object);
    }

    [Fact]
    public async Task IssueAsync_CreatesCardWithBalanceEqualToAmount()
    {
        _uow.Setup(u => u.GiftCardStore.CreateAsync(It.IsAny<GiftCard>()))
            .ReturnsAsync((GiftCard g) => g);

        var dto = await _sut.IssueAsync(new IssueGiftCardRequest { Amount = 200000 });

        dto.Balance.Should().Be(200000);
        dto.InitialBalance.Should().Be(200000);
        dto.Code.Should().StartWith("GC-");
    }

    [Fact]
    public async Task IssueAsync_NonPositiveAmount_Throws()
    {
        await FluentActions.Awaiting(() => _sut.IssueAsync(new IssueGiftCardRequest { Amount = 0 }))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ValidateAsync_ActiveCardWithBalance_ReturnsValid()
    {
        _uow.Setup(u => u.GiftCardStore.GetByCodeAsync("GC-X"))
            .ReturnsAsync(new GiftCard { Code = "GC-X", IsActive = true, Balance = 50000 });

        var r = await _sut.ValidateAsync("GC-X");

        r.Valid.Should().BeTrue();
        r.Balance.Should().Be(50000);
    }

    [Fact]
    public async Task ValidateAsync_DepletedCard_ReturnsInvalid()
    {
        _uow.Setup(u => u.GiftCardStore.GetByCodeAsync("GC-Y"))
            .ReturnsAsync(new GiftCard { Code = "GC-Y", IsActive = true, Balance = 0 });

        var r = await _sut.ValidateAsync("GC-Y");

        r.Valid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ExpiredCard_ReturnsInvalid()
    {
        _uow.Setup(u => u.GiftCardStore.GetByCodeAsync("GC-Z"))
            .ReturnsAsync(new GiftCard { Code = "GC-Z", IsActive = true, Balance = 10000, ExpiresAt = DateTime.UtcNow.AddDays(-1) });

        var r = await _sut.ValidateAsync("GC-Z");

        r.Valid.Should().BeFalse();
    }
}
