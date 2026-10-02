using System.Linq.Expressions;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class PatronCategoryServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly PatronCategoryManager _sut;

    private static readonly Guid TheaterId  = Guid.NewGuid();
    private static readonly Guid StandardId = Guid.NewGuid();
    private static readonly Guid DoubleId   = Guid.NewGuid();

    public PatronCategoryServiceTests()
    {
        _sut = new PatronCategoryManager(_uowMock.Object);

        _uowMock.Setup(u => u.SeatTypeStore.ExistsAsync(It.IsAny<Expression<Func<SeatType, bool>>>()))
            .ReturnsAsync(true);
        _uowMock.Setup(u => u.PatronCategoryStore.ExistsAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(false);
        _uowMock.Setup(u => u.SeatTypeStore.FindAsync(It.IsAny<Expression<Func<SeatType, bool>>>()))
            .ReturnsAsync(new List<SeatType>());
    }

    private static PatronCategory MakeEntity(string name, Guid seatTypeId, double price = 90000, bool isActive = true)
    {
        return new PatronCategory
        {
            Id          = Guid.NewGuid(),
            TheaterId   = TheaterId,
            SeatTypeId  = seatTypeId,
            Name        = name,
            Price       = price,
            IsActive    = isActive,
        };
    }

    [Fact]
    public async Task CreateAsync_CreatesExactlyOneRow()
    {
        var request = new CreatePatronCategoryRequest
        {
            TheaterId  = TheaterId,
            Name       = "Adult",
            SeatTypeId = StandardId,
            Price      = 90000,
            IsActive   = true,
        };

        var dto = await _sut.CreateAsync(request);

        dto.Name.Should().Be("Adult");
        dto.SeatTypeId.Should().Be(StandardId);
        dto.Price.Should().Be(90000);
        _uowMock.Verify(u => u.PatronCategoryStore.CreateAsync(It.IsAny<PatronCategory>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_SeatTypeFromAnotherTheater_Throws()
    {
        _uowMock.Setup(u => u.SeatTypeStore.ExistsAsync(It.IsAny<Expression<Func<SeatType, bool>>>()))
            .ReturnsAsync(false);

        var request = new CreatePatronCategoryRequest { TheaterId = TheaterId, Name = "Adult", SeatTypeId = StandardId, Price = 90000 };

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _uowMock.Verify(u => u.PatronCategoryStore.CreateAsync(It.IsAny<PatronCategory>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DuplicateNameAndSeatType_Throws()
    {
        _uowMock.Setup(u => u.PatronCategoryStore.ExistsAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(true);

        var request = new CreatePatronCategoryRequest { TheaterId = TheaterId, Name = "Adult", SeatTypeId = StandardId, Price = 90000 };

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _uowMock.Verify(u => u.PatronCategoryStore.CreateAsync(It.IsAny<PatronCategory>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesOnlyTargetRow()
    {
        var standard = MakeEntity("Adult", StandardId, price: 90000);
        var doubleRow = MakeEntity("Adult", DoubleId, price: 171000);
        _uowMock.Setup(u => u.PatronCategoryStore.GetByIdAsync(standard.Id)).ReturnsAsync(standard);

        var request = new UpdatePatronCategoryRequest
        {
            Id         = standard.Id,
            Name       = "Adult+",
            SeatTypeId = StandardId,
            Price      = 120000,
            IsActive   = true,
        };

        var dto = await _sut.UpdateAsync(request);

        dto.Name.Should().Be("Adult+");
        dto.Price.Should().Be(120000);
        _uowMock.Verify(u => u.PatronCategoryStore.UpdateAsync(standard), Times.Once);
        _uowMock.Verify(u => u.PatronCategoryStore.DeleteAsync(It.IsAny<PatronCategory>()), Times.Never);
        doubleRow.Name.Should().Be("Adult");
        doubleRow.Price.Should().Be(171000);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ThrowsKeyNotFound()
    {
        var unknownId = Guid.NewGuid();
        _uowMock.Setup(u => u.PatronCategoryStore.GetByIdAsync(unknownId)).ReturnsAsync((PatronCategory?)null);

        var request = new UpdatePatronCategoryRequest { Id = unknownId, Name = "Adult", SeatTypeId = StandardId, Price = 90000 };

        var act = () => _sut.UpdateAsync(request);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_CanChangeSeatType()
    {
        var entity = MakeEntity("Adult", StandardId, price: 90000);
        _uowMock.Setup(u => u.PatronCategoryStore.GetByIdAsync(entity.Id)).ReturnsAsync(entity);

        var request = new UpdatePatronCategoryRequest { Id = entity.Id, Name = "Adult", SeatTypeId = DoubleId, Price = 171000 };

        var dto = await _sut.UpdateAsync(request);

        dto.SeatTypeId.Should().Be(DoubleId);
        _uowMock.Verify(u => u.PatronCategoryStore.CreateAsync(It.IsAny<PatronCategory>()), Times.Never);
        _uowMock.Verify(u => u.PatronCategoryStore.UpdateAsync(entity), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateAfterChange_Throws()
    {
        var entity = MakeEntity("Adult", DoubleId, price: 171000);
        _uowMock.Setup(u => u.PatronCategoryStore.GetByIdAsync(entity.Id)).ReturnsAsync(entity);
        _uowMock.Setup(u => u.PatronCategoryStore.ExistsAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(true);

        var request = new UpdatePatronCategoryRequest { Id = entity.Id, Name = "Student", SeatTypeId = StandardId, Price = 65000 };

        var act = () => _sut.UpdateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _uowMock.Verify(u => u.PatronCategoryStore.UpdateAsync(It.IsAny<PatronCategory>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_DeletesOnlyTargetRow()
    {
        var standard = MakeEntity("Adult", StandardId);
        _uowMock.Setup(u => u.PatronCategoryStore.GetByIdAsync(standard.Id)).ReturnsAsync(standard);

        await _sut.DeleteAsync(standard.Id);

        _uowMock.Verify(u => u.PatronCategoryStore.DeleteAsync(standard), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ThrowsKeyNotFound()
    {
        var unknownId = Guid.NewGuid();
        _uowMock.Setup(u => u.PatronCategoryStore.GetByIdAsync(unknownId)).ReturnsAsync((PatronCategory?)null);

        var act = () => _sut.DeleteAsync(unknownId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uowMock.Verify(u => u.PatronCategoryStore.DeleteAsync(It.IsAny<PatronCategory>()), Times.Never);
    }
}
