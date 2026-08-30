using Cinema.Business.DTO.Movies;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class MovieServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly MovieManager _sut;

    public MovieServiceTests()
    {
        _sut = new MovieManager(_uowMock.Object);
    }

    [Fact]
    public async Task GetMoviesAsync_ReturnsMappedResults()
    {
        var movieId = Guid.NewGuid();
        var movies  = new List<Movie> { new() { Id = movieId, Title = "Movie A" } };
        _uowMock.Setup(u => u.MovieStore.GetPagedAsync(null, null, (Guid?)null, 1, 12)).ReturnsAsync((movies, 1));
        // Ratings for the whole page come from one grouped lookup, not one call per movie.
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, double> { [movieId] = 4.5 });

        var result = await _sut.GetMoviesAsync(new PagingSearchDTO { PageIndex = 1, PageSize = 12 });

        result.TotalCount.Should().Be(1);
        result.Results.Should().HaveCount(1);
        result.Results.Single().AverageRating.Should().Be(4.5);
        _uowMock.Verify(u => u.MovieStore.GetAverageRatingAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetDetailAsync_ExistingMovie_ReturnsMappedDetail()
    {
        var movieId = Guid.NewGuid();
        var movie   = new Movie
        {
            Id          = movieId,
            Title       = "Test Movie",
            Evaluations = new List<Evaluation>(),
            Comments    = new List<Comment>()
        };
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingAsync(movieId)).ReturnsAsync(4.0);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatCountsByMovieAsync(movieId))
            .ReturnsAsync(new Dictionary<(Guid, Guid), int>());
        _uowMock.Setup(u => u.CommentStore.GetRecentForMovieAsync(movieId, 10))
            .ReturnsAsync(new List<CommentView>());

        var result = await _sut.GetDetailAsync(movieId);

        result.Should().NotBeNull();
        result.Id.Should().Be(movieId);
        result.AverageRating.Should().Be(4.0);
    }

    [Fact]
    public async Task GetDetailAsync_NotFound_Throws()
    {
        var missingId = Guid.NewGuid();
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(missingId)).ReturnsAsync((Movie?)null);

        await _sut.Invoking(s => s.GetDetailAsync(missingId))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetNowShowingAsync_ReturnsMappedList()
    {
        var movies = new List<Movie> { new() { Id = Guid.NewGuid(), Title = "Now Showing" } };
        _uowMock.Setup(u => u.MovieStore.GetNowShowingAsync()).ReturnsAsync(movies);

        var result = await _sut.GetNowShowingAsync(new PagingSearchDTO());

        result.Results.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteAsync_ExistingMovie_SetsIsActiveFalse()
    {
        var movieId = Guid.NewGuid();
        var movie   = new Movie { Id = movieId, Title = "To Delete", IsActive = true };
        _uowMock.Setup(u => u.MovieStore.GetByIdAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.UpdateAsync(movie)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        await _sut.DeleteAsync(movieId);

        movie.IsActive.Should().BeFalse();
        _uowMock.Verify(u => u.MovieStore.UpdateAsync(movie), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        var missingId = Guid.NewGuid();
        _uowMock.Setup(u => u.MovieStore.GetByIdAsync(missingId)).ReturnsAsync((Movie?)null);

        await _sut.Invoking(s => s.DeleteAsync(missingId))
            .Should().ThrowAsync<KeyNotFoundException>();
    }
}
