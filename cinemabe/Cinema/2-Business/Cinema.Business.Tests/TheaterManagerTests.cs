using Cinema.Business.Contracts;
using Cinema.Business.DTO.Theaters;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class TheaterManagerTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly TheaterManager _sut;

    public TheaterManagerTests()
    {
        _sut = new TheaterManager(_uowMock.Object, new Mock<ISeatTypeManager>().Object);
    }

    private static UpdateTheaterRequest Update(int seats = 10)
    {
        return new UpdateTheaterRequest { Id = Guid.NewGuid(), Name = "T", MaxSeatsPerBooking = seats };
    }

    private void SetupExisting(UpdateTheaterRequest request)
    {
        _uowMock.Setup(u => u.TheaterStore.GetByIdAsync(request.Id)).ReturnsAsync(new Theater { Id = request.Id });
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
}
