using Cinema.Business.DTO.Catalog;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class TimeSlotManagerTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly TimeSlotManager _sut;
    private readonly Guid _theaterId = Guid.NewGuid();

    public TimeSlotManagerTests()
    {
        _sut = new TimeSlotManager(_uowMock.Object);
        _uowMock.Setup(u => u.TimeSlotStore.HasTimeSlotOverlapAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync(false);
    }

    private CreateTimeSlotRequest CreateRequest(string start = "09:00", string end = "11:00")
        => new() { TheaterId = _theaterId, Name = "Morning", StartTime = start, EndTime = end };

    [Fact]
    public async Task Create_Overlapping_Throws()
    {
        _uowMock.Setup(u => u.TimeSlotStore.HasTimeSlotOverlapAsync(_theaterId, "09:00", "11:00", null)).ReturnsAsync(true);

        var act = () => _sut.CreateAsync(CreateRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
        _uowMock.Verify(u => u.TimeSlotStore.CreateAsync(It.IsAny<TimeSlot>()), Times.Never);
    }

    [Fact]
    public async Task Create_NoOverlap_Saves()
    {
        await _sut.CreateAsync(CreateRequest());

        _uowMock.Verify(u => u.TimeSlotStore.HasTimeSlotOverlapAsync(_theaterId, "09:00", "11:00", null), Times.Once);
        _uowMock.Verify(u => u.TimeSlotStore.CreateAsync(It.IsAny<TimeSlot>()), Times.Once);
    }

    [Fact]
    public async Task Update_Overlapping_Throws()
    {
        var existing = new TimeSlot { Id = Guid.NewGuid(), TheaterId = _theaterId, Name = "Morning", StartTime = "08:00", EndTime = "10:00" };
        _uowMock.Setup(u => u.TimeSlotStore.GetByIdAsync(existing.Id)).ReturnsAsync(existing);
        _uowMock.Setup(u => u.TimeSlotStore.HasTimeSlotOverlapAsync(_theaterId, "09:00", "11:00", existing.Id)).ReturnsAsync(true);

        var act = () => _sut.UpdateAsync(new UpdateTimeSlotRequest { Id = existing.Id, TheaterId = _theaterId, Name = "Morning", StartTime = "09:00", EndTime = "11:00" });

        await act.Should().ThrowAsync<InvalidOperationException>();
        _uowMock.Verify(u => u.TimeSlotStore.UpdateAsync(It.IsAny<TimeSlot>()), Times.Never);
    }

    [Fact]
    public async Task Update_PassesOwnIdAsExclusion()
    {
        var existing = new TimeSlot { Id = Guid.NewGuid(), TheaterId = _theaterId, Name = "Morning", StartTime = "08:00", EndTime = "10:00" };
        _uowMock.Setup(u => u.TimeSlotStore.GetByIdAsync(existing.Id)).ReturnsAsync(existing);

        await _sut.UpdateAsync(new UpdateTimeSlotRequest { Id = existing.Id, TheaterId = _theaterId, Name = "Morning", StartTime = "09:00", EndTime = "11:00" });

        _uowMock.Verify(u => u.TimeSlotStore.HasTimeSlotOverlapAsync(_theaterId, "09:00", "11:00", existing.Id), Times.Once);
        _uowMock.Verify(u => u.TimeSlotStore.UpdateAsync(It.IsAny<TimeSlot>()), Times.Once);
    }

    [Theory]
    [InlineData("12:00", "12:00")]
    [InlineData("14:00", "10:00")]
    public async Task Create_EndNotAfterStart_Throws(string start, string end)
    {
        var act = () => _sut.CreateAsync(CreateRequest(start, end));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _uowMock.Verify(u => u.TimeSlotStore.HasTimeSlotOverlapAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Theory]
    [InlineData("9:00")]
    [InlineData("25:00")]
    [InlineData("abc")]
    public async Task Create_BadFormat_Throws(string start)
    {
        var act = () => _sut.CreateAsync(CreateRequest(start, "11:00"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
