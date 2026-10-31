using Cinema.Business.DTO.Catalog;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class PersonManagerTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly PersonManager _sut;

    public PersonManagerTests()
    {
        _sut = new PersonManager(_uowMock.Object);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        _uowMock.Setup(u => u.PersonStore.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Person, bool>>>()))
            .ReturnsAsync(true);

        await FluentActions.Awaiting(() => _sut.CreateAsync(new CreatePersonRequest
        {
            Name = "Cillian Murphy"
        })).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_TrimsName()
    {
        _uowMock.Setup(u => u.PersonStore.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Person, bool>>>()))
            .ReturnsAsync(false);
        _uowMock.Setup(u => u.PersonStore.CreateAsync(It.IsAny<Person>())).ReturnsAsync((Person p) => p);

        var result = await _sut.CreateAsync(new CreatePersonRequest
        {
            Name = "  Cillian Murphy  "
        });

        result.Name.Should().Be("Cillian Murphy");
    }

    [Fact]
    public async Task UpdateAsync_RenameToExistingName_Throws()
    {
        var entity = new Person { Id = Guid.NewGuid(), Name = "Original" };
        _uowMock.Setup(u => u.PersonStore.GetByIdAsync(entity.Id)).ReturnsAsync(entity);
        _uowMock.Setup(u => u.PersonStore.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Person, bool>>>()))
            .ReturnsAsync(true);

        await FluentActions.Awaiting(() => _sut.UpdateAsync(new UpdatePersonRequest
        {
            Id = entity.Id, Name = "Someone Else"
        })).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetByNamesAsync_TrimsDeduplicatesAndFiltersBlanks()
    {
        List<string>? capturedNames = null;
        _uowMock.Setup(u => u.PersonStore.GetWithPhotoByNamesAsync(It.IsAny<IReadOnlyCollection<string>>()))
            .Callback<IReadOnlyCollection<string>>(names => capturedNames = names.ToList())
            .ReturnsAsync(new List<Person>());

        await _sut.GetByNamesAsync(new List<string> { " Cillian Murphy ", "cillian murphy", "", "   ", "Anne Hathaway" });

        capturedNames.Should().NotBeNull();
        capturedNames!.Should().ContainSingle(n => n == "Cillian Murphy" || n == "cillian murphy");
        capturedNames.Should().Contain("Anne Hathaway");
        capturedNames.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByNamesAsync_TooManyNames_Throws()
    {
        var names = Enumerable.Range(0, 51).Select(i => $"Person {i}").ToList();

        await FluentActions.Awaiting(() => _sut.GetByNamesAsync(names))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetByNamesAsync_EmptyList_ReturnsEmptyWithoutCallingStore()
    {
        var result = await _sut.GetByNamesAsync(new List<string>());

        result.Should().BeEmpty();
        _uowMock.Verify(u => u.PersonStore.GetWithPhotoByNamesAsync(It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_PersonWithCredits_ThrowsAndDoesNotDelete()
    {
        var id = Guid.NewGuid();
        _uowMock.Setup(u => u.MovieCreditStore.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MovieCredit, bool>>>()))
            .ReturnsAsync(true);

        await FluentActions.Awaiting(() => _sut.DeleteAsync(id))
            .Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.PersonStore.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_PersonWithoutCredits_Deletes()
    {
        var id = Guid.NewGuid();
        _uowMock.Setup(u => u.MovieCreditStore.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MovieCredit, bool>>>()))
            .ReturnsAsync(false);
        _uowMock.Setup(u => u.PersonStore.DeleteAsync(id)).ReturnsAsync(new Person { Id = id });

        await _sut.DeleteAsync(id);

        _uowMock.Verify(u => u.PersonStore.DeleteAsync(id), Times.Once);
    }
}
