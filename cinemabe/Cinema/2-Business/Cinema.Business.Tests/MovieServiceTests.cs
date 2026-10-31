using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Movies;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class MovieServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly Mock<IExternalRatingsProvider> _externalRatingsMock = new();
    private readonly MovieManager _sut;

    public MovieServiceTests()
    {
        _sut = new MovieManager(_uowMock.Object, _externalRatingsMock.Object);
        // Default: no credits set up for any movie, so existing GetDetailAsync tests that don't care
        // about credits don't need to stub this explicitly.
        _uowMock.Setup(u => u.MovieCreditStore.GetByMovieAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<MovieCreditRow>());
        // Default: no external ratings, so existing GetDetailAsync tests don't need to stub this.
        _externalRatingsMock.Setup(p => p.GetRatingsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalRatingsDTO?)null);
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
        _uowMock.Setup(u => u.ShowTimeStore.GetMovieScheduleAsync(movieId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MovieScheduleRow>());
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatCountsByMovieAsync(movieId))
            .ReturnsAsync(new Dictionary<(Guid, Guid), int>());
        _uowMock.Setup(u => u.CommentStore.GetRecentForMovieAsync(movieId, 10))
            .ReturnsAsync(new List<CommentView>());

        var result = await _sut.GetDetailAsync(movieId, null);

        result.Should().NotBeNull();
        result.Id.Should().Be(movieId);
        result.AverageRating.Should().Be(4.0);
    }

    [Fact]
    public async Task GetDetailAsync_Anonymous_CanReviewFalse_AndSkipsEligibilityQuery()
    {
        var movieId = Guid.NewGuid();
        var movie   = new Movie { Id = movieId, Title = "Test Movie", Evaluations = new List<Evaluation>() };
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingAsync(movieId)).ReturnsAsync(0);
        _uowMock.Setup(u => u.CommentStore.GetRecentForMovieAsync(movieId, 10)).ReturnsAsync(new List<CommentView>());
        _uowMock.Setup(u => u.ShowTimeStore.GetMovieScheduleAsync(movieId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MovieScheduleRow>());
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatCountsByMovieAsync(movieId))
            .ReturnsAsync(new Dictionary<(Guid, Guid), int>());

        var result = await _sut.GetDetailAsync(movieId, null);

        result.CanReview.Should().BeFalse();
        _uowMock.Verify(u => u.InvoiceStore.HasWatchedMovieAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task GetDetailAsync_UserWhoWatched_CanReviewTrue()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var movie   = new Movie { Id = movieId, Title = "Test Movie", Evaluations = new List<Evaluation>() };
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingAsync(movieId)).ReturnsAsync(0);
        _uowMock.Setup(u => u.CommentStore.GetRecentForMovieAsync(movieId, 10)).ReturnsAsync(new List<CommentView>());
        _uowMock.Setup(u => u.ShowTimeStore.GetMovieScheduleAsync(movieId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MovieScheduleRow>());
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatCountsByMovieAsync(movieId))
            .ReturnsAsync(new Dictionary<(Guid, Guid), int>());
        _uowMock.Setup(u => u.InvoiceStore.HasWatchedMovieAsync(userId, movieId, It.IsAny<DateTime>()))
            .ReturnsAsync(true);

        var result = await _sut.GetDetailAsync(movieId, userId);

        result.CanReview.Should().BeTrue();
    }

    [Fact]
    public async Task RateMovieAsync_NotWatched_ThrowsAccessDenied_AndDoesNotWrite()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        _uowMock.Setup(u => u.InvoiceStore.HasWatchedMovieAsync(userId, movieId, It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        await _sut.Invoking(s => s.RateMovieAsync(movieId, userId, 5, null))
            .Should().ThrowAsync<AccessDeniedException>();

        _uowMock.Verify(u => u.EvaluationStore.CreateAsync(It.IsAny<Evaluation>()), Times.Never);
        _uowMock.Verify(u => u.EvaluationStore.UpdateAsync(It.IsAny<Evaluation>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task RateMovieAsync_Watched_Upserts()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        _uowMock.Setup(u => u.InvoiceStore.HasWatchedMovieAsync(userId, movieId, It.IsAny<DateTime>()))
            .ReturnsAsync(true);
        _uowMock.Setup(u => u.EvaluationStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Evaluation, bool>>>()))
            .ReturnsAsync(new List<Evaluation>());

        var score = await _sut.RateMovieAsync(movieId, userId, 5, "Great");

        score.Should().Be(5);
        _uowMock.Verify(u => u.EvaluationStore.CreateAsync(It.IsAny<Evaluation>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddCommentAsync_NotWatched_ThrowsAccessDenied_AndDoesNotCreate()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        _uowMock.Setup(u => u.InvoiceStore.HasWatchedMovieAsync(userId, movieId, It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        await _sut.Invoking(s => s.AddCommentAsync(movieId, userId, "Nice movie", null))
            .Should().ThrowAsync<AccessDeniedException>();

        _uowMock.Verify(u => u.CommentStore.CreateAsync(It.IsAny<Comment>()), Times.Never);
    }

    [Fact]
    public async Task AddCommentAsync_Watched_CreatesComment()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        _uowMock.Setup(u => u.InvoiceStore.HasWatchedMovieAsync(userId, movieId, It.IsAny<DateTime>()))
            .ReturnsAsync(true);
        _uowMock.Setup(u => u.CommentStore.CreateAsync(It.IsAny<Comment>()))
            .ReturnsAsync((Comment c) => c);

        var result = await _sut.AddCommentAsync(movieId, userId, "Nice movie", null);

        result.Content.Should().Be("Nice movie");
        _uowMock.Verify(u => u.CommentStore.CreateAsync(It.IsAny<Comment>()), Times.Once);
    }

    [Fact]
    public async Task GetDetailAsync_RequestsFourDayWindowFromNow()
    {
        var movieId = Guid.NewGuid();
        var movie   = new Movie { Id = movieId, Title = "Test Movie", Evaluations = new List<Evaluation>() };
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingAsync(movieId)).ReturnsAsync(0);
        _uowMock.Setup(u => u.CommentStore.GetRecentForMovieAsync(movieId, 10)).ReturnsAsync(new List<CommentView>());
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatCountsByMovieAsync(movieId))
            .ReturnsAsync(new Dictionary<(Guid, Guid), int>());

        DateTime capturedFrom = default, capturedTo = default;
        _uowMock.Setup(u => u.ShowTimeStore.GetMovieScheduleAsync(movieId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<Guid, DateTime, DateTime>((_, from, to) => { capturedFrom = from; capturedTo = to; })
            .ReturnsAsync(new List<MovieScheduleRow>());

        await _sut.GetDetailAsync(movieId, null);

        capturedFrom.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(2));
        capturedTo.Should().Be(DateTime.Today.AddDays(4));
    }

    [Fact]
    public async Task GetDetailAsync_MapsScheduleRowsToSummaries_SubtractingBookedSeats()
    {
        var movieId   = Guid.NewGuid();
        var showTime  = Guid.NewGuid();
        var roomA     = Guid.NewGuid();
        var roomB     = Guid.NewGuid();
        var theaterA  = Guid.NewGuid();
        var theaterB  = Guid.NewGuid();
        var movie    = new Movie { Id = movieId, Title = "Test Movie", Evaluations = new List<Evaluation>() };
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingAsync(movieId)).ReturnsAsync(0);
        _uowMock.Setup(u => u.CommentStore.GetRecentForMovieAsync(movieId, 10)).ReturnsAsync(new List<CommentView>());

        var rows = new List<MovieScheduleRow>
        {
            new(showTime, DateTime.Today.AddHours(20), DateTime.Today.AddHours(22), ProjectionForm.TwoD,
                roomA, "Room A", "Standard", theaterA, "Cinema One", "1 Alpha St, District 1", 100),
            new(showTime, DateTime.Today.AddHours(20), DateTime.Today.AddHours(22), ProjectionForm.ThreeD,
                roomB, "Room B", "IMAX", theaterB, "Cinema Two", "2 Beta Rd, District 3", 50),
        };
        _uowMock.Setup(u => u.ShowTimeStore.GetMovieScheduleAsync(movieId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(rows);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatCountsByMovieAsync(movieId))
            .ReturnsAsync(new Dictionary<(Guid, Guid), int> { [(showTime, roomA)] = 30 });

        var result = await _sut.GetDetailAsync(movieId, null);

        result.ShowTimes.Should().HaveCount(2);
        var a = result.ShowTimes.Single(x => x.RoomId == roomA);
        a.TheaterId.Should().Be(theaterA);
        a.TheaterName.Should().Be("Cinema One");
        a.TheaterAddress.Should().Be("1 Alpha St, District 1");
        a.AvailableSeats.Should().Be(70);
        var b = result.ShowTimes.Single(x => x.RoomId == roomB);
        b.TheaterId.Should().Be(theaterB);
        b.TheaterName.Should().Be("Cinema Two");
        b.TheaterAddress.Should().Be("2 Beta Rd, District 3");
        b.AvailableSeats.Should().Be(50);
    }

    [Fact]
    public async Task GetDetailAsync_NotFound_Throws()
    {
        var missingId = Guid.NewGuid();
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(missingId)).ReturnsAsync((Movie?)null);

        await _sut.Invoking(s => s.GetDetailAsync(missingId, null))
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

    [Fact]
    public async Task CreateAsync_FreeBaseSlug_UsesBaseSlug()
    {
        Movie? created = null;
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        _uowMock.Setup(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()))
            .Callback<Movie>(m => created = m)
            .ReturnsAsync((Movie m) => m);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(It.IsAny<Guid>()))
            .ReturnsAsync(() => created);

        await _sut.CreateAsync(new CreateMovieRequest { Title = "Wicked" });

        created.Should().NotBeNull();
        created!.Slug.Should().Be("wicked");
    }

    [Fact]
    public async Task CreateAsync_TakenBaseSlug_AppendsSuffix()
    {
        Movie? created = null;
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string> { "wicked" });
        _uowMock.Setup(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()))
            .Callback<Movie>(m => created = m)
            .ReturnsAsync((Movie m) => m);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(It.IsAny<Guid>()))
            .ReturnsAsync(() => created);

        await _sut.CreateAsync(new CreateMovieRequest { Title = "Wicked" });

        created!.Slug.Should().Be("wicked-2");
    }

    [Fact]
    public async Task CreateAsync_TrimsImdbId()
    {
        Movie? created = null;
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        _uowMock.Setup(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()))
            .Callback<Movie>(m => created = m)
            .ReturnsAsync((Movie m) => m);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(It.IsAny<Guid>()))
            .ReturnsAsync(() => created);

        await _sut.CreateAsync(new CreateMovieRequest { Title = "Wicked", ImdbId = "  tt0133093  " });

        created!.ImdbId.Should().Be("tt0133093");
    }

    [Fact]
    public async Task UpdateAsync_TitleChanged_SlugUnchanged()
    {
        var movieId = Guid.NewGuid();
        var movie = new Movie { Id = movieId, Title = "Wicked", Slug = "wicked", MovieTypeDetails = new List<MovieTypeDetail>() };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);

        await _sut.UpdateAsync(new UpdateMovieRequest { Id = movieId, Title = "Wicked: Part One" });

        movie.Slug.Should().Be("wicked");
    }

    [Fact]
    public async Task UpdateAsync_BlankImdbId_ClearsExistingValue()
    {
        var movieId = Guid.NewGuid();
        var movie = new Movie { Id = movieId, Title = "Wicked", Slug = "wicked", ImdbId = "tt0133093", MovieTypeDetails = new List<MovieTypeDetail>() };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);

        await _sut.UpdateAsync(new UpdateMovieRequest { Id = movieId, Title = "Wicked", ImdbId = "" });

        movie.ImdbId.Should().BeNull();
    }

    [Fact]
    public async Task GetDetailBySlugAsync_ExistingSlug_ReturnsMappedDetail()
    {
        var movieId = Guid.NewGuid();
        var movie = new Movie { Id = movieId, Title = "Wicked", Slug = "wicked", Evaluations = new List<Evaluation>() };
        _uowMock.Setup(u => u.MovieStore.GetDetailBySlugAsync("wicked")).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingAsync(movieId)).ReturnsAsync(0);
        _uowMock.Setup(u => u.CommentStore.GetRecentForMovieAsync(movieId, 10)).ReturnsAsync(new List<CommentView>());
        _uowMock.Setup(u => u.ShowTimeStore.GetMovieScheduleAsync(movieId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MovieScheduleRow>());
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatCountsByMovieAsync(movieId))
            .ReturnsAsync(new Dictionary<(Guid, Guid), int>());

        var result = await _sut.GetDetailBySlugAsync("Wicked", null);

        result.Id.Should().Be(movieId);
        result.Slug.Should().Be("wicked");
    }

    [Fact]
    public async Task GetDetailBySlugAsync_UnknownSlug_Throws()
    {
        _uowMock.Setup(u => u.MovieStore.GetDetailBySlugAsync("nope")).ReturnsAsync((Movie?)null);

        await _sut.Invoking(s => s.GetDetailBySlugAsync("nope", null))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── MovieCredit ──────────────────────────────────────────────────────────

    private void SetupBasicDetailRead(Guid movieId, Movie movie)
    {
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingAsync(movieId)).ReturnsAsync(0);
        _uowMock.Setup(u => u.CommentStore.GetRecentForMovieAsync(movieId, 10)).ReturnsAsync(new List<CommentView>());
        _uowMock.Setup(u => u.ShowTimeStore.GetMovieScheduleAsync(movieId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MovieScheduleRow>());
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatCountsByMovieAsync(movieId))
            .ReturnsAsync(new Dictionary<(Guid, Guid), int>());
    }

    /// <summary>Stubs the single batched read NormalizeCreditsAsync does against PersonStore.</summary>
    private void SetupPeople(params Person[] people)
    {
        _uowMock.Setup(u => u.PersonStore.FindAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Person, bool>>>()))
            .ReturnsAsync(people.AsEnumerable());
    }

    [Fact]
    public async Task GetDetailAsync_MapsCreditsFromStore()
    {
        var movieId  = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var movie    = new Movie { Id = movieId, Title = "Test Movie", Evaluations = new List<Evaluation>() };
        SetupBasicDetailRead(movieId, movie);
        _uowMock.Setup(u => u.MovieCreditStore.GetByMovieAsync(movieId)).ReturnsAsync(new List<MovieCreditRow>
        {
            new(Guid.NewGuid(), personId, "Christopher Nolan", "photo.jpg", CreditType.Director, null, 0),
        });

        var result = await _sut.GetDetailAsync(movieId, null);

        result.Credits.Should().ContainSingle();
        var credit = result.Credits.Single();
        credit.PersonId.Should().Be(personId);
        credit.PersonName.Should().Be("Christopher Nolan");
        credit.PersonPhotoUrl.Should().Be("photo.jpg");
        credit.CreditType.Should().Be(CreditType.Director);
    }

    [Fact]
    public async Task GetDetailAsync_NoCredits_ReturnsEmptyList()
    {
        var movieId = Guid.NewGuid();
        var movie   = new Movie { Id = movieId, Title = "Test Movie", Evaluations = new List<Evaluation>() };
        SetupBasicDetailRead(movieId, movie);

        var result = await _sut.GetDetailAsync(movieId, null);

        result.Credits.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDetailAsync_ExternalRatingsFound_SetsExternalRatings()
    {
        var movieId = Guid.NewGuid();
        var releaseDate = new DateOnly(2010, 7, 16);
        var movie = new Movie { Id = movieId, Title = "Inception", ReleaseDate = releaseDate, Evaluations = new List<Evaluation>() };
        SetupBasicDetailRead(movieId, movie);
        var ratings = new ExternalRatingsDTO { RottenTomatoes = "87%", ImdbRating = "8.8", Metascore = "74" };
        _externalRatingsMock.Setup(p => p.GetRatingsAsync("Inception", 2010, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ratings);

        var result = await _sut.GetDetailAsync(movieId, null);

        result.ExternalRatings.Should().BeSameAs(ratings);
    }

    [Fact]
    public async Task GetDetailAsync_PassesMovieImdbIdToProvider()
    {
        var movieId = Guid.NewGuid();
        var releaseDate = new DateOnly(2010, 7, 16);
        var movie = new Movie { Id = movieId, Title = "Inception", ReleaseDate = releaseDate, ImdbId = "tt1375666", Evaluations = new List<Evaluation>() };
        SetupBasicDetailRead(movieId, movie);

        await _sut.GetDetailAsync(movieId, null);

        _externalRatingsMock.Verify(p => p.GetRatingsAsync("Inception", 2010, "tt1375666", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDetailAsync_ExternalRatingsNotFound_LeavesExternalRatingsNull()
    {
        var movieId = Guid.NewGuid();
        var movie = new Movie { Id = movieId, Title = "Test Movie", Evaluations = new List<Evaluation>() };
        SetupBasicDetailRead(movieId, movie);

        var result = await _sut.GetDetailAsync(movieId, null);

        result.ExternalRatings.Should().BeNull();
        result.Credits.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMoviesAsync_NeverCallsExternalRatingsProvider()
    {
        var movieId = Guid.NewGuid();
        var movies  = new List<Movie> { new() { Id = movieId, Title = "Movie A" } };
        _uowMock.Setup(u => u.MovieStore.GetPagedAsync(null, null, (Guid?)null, 1, 12)).ReturnsAsync((movies, 1));
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, double>());

        await _sut.GetMoviesAsync(new PagingSearchDTO { PageIndex = 1, PageSize = 12 });

        _externalRatingsMock.Verify(p => p.GetRatingsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithCredits_AttachesNormalizedCredits()
    {
        var personId = Guid.NewGuid();
        Movie? created = null;
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        SetupPeople(new Person { Id = personId, Name = "Christopher Nolan" });
        _uowMock.Setup(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()))
            .Callback<Movie>(m => created = m)
            .ReturnsAsync((Movie m) => m);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(It.IsAny<Guid>())).ReturnsAsync(() => created);

        await _sut.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonId = personId, PersonName = "Christopher Nolan", CreditType = CreditType.Director, CharacterName = "ignored", DisplayOrder = 0 },
            },
        });

        created!.MovieCredits.Should().ContainSingle();
        var credit = created.MovieCredits.Single();
        credit.PersonId.Should().Be(personId);
        credit.CreditType.Should().Be(CreditType.Director);
        credit.CharacterName.Should().BeNull("Director credits never carry a character name");
    }

    [Fact]
    public async Task CreateAsync_NullCredits_CreatesNone()
    {
        Movie? created = null;
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        _uowMock.Setup(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()))
            .Callback<Movie>(m => created = m)
            .ReturnsAsync((Movie m) => m);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(It.IsAny<Guid>())).ReturnsAsync(() => created);

        await _sut.CreateAsync(new CreateMovieRequest { Title = "Wicked" });

        created!.MovieCredits.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_NullCredits_LeavesExistingCreditsUntouched()
    {
        var movieId = Guid.NewGuid();
        var existingCredit = new MovieCredit { Id = Guid.NewGuid(), MovieId = movieId, PersonId = Guid.NewGuid(), CreditType = CreditType.Actor };
        var movie = new Movie
        {
            Id = movieId, Title = "Wicked", Slug = "wicked",
            MovieTypeDetails = new List<MovieTypeDetail>(),
            MovieCredits = new List<MovieCredit> { existingCredit },
        };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);

        await _sut.UpdateAsync(new UpdateMovieRequest { Id = movieId, Title = "Wicked", Credits = null });

        movie.MovieCredits.Should().ContainSingle().Which.Should().BeSameAs(existingCredit);
    }

    [Fact]
    public async Task UpdateAsync_ReconcilesCredits()
    {
        var movieId = Guid.NewGuid();
        var personA = Guid.NewGuid();
        var personB = Guid.NewGuid();
        var personC = Guid.NewGuid();
        var creditA = new MovieCredit { Id = Guid.NewGuid(), MovieId = movieId, PersonId = personA, CreditType = CreditType.Actor, DisplayOrder = 0 };
        var creditB = new MovieCredit { Id = Guid.NewGuid(), MovieId = movieId, PersonId = personB, CreditType = CreditType.Director, DisplayOrder = 0 };
        var movie = new Movie
        {
            Id = movieId, Title = "Wicked", Slug = "wicked",
            MovieTypeDetails = new List<MovieTypeDetail>(),
            MovieCredits = new List<MovieCredit> { creditA, creditB },
        };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        SetupPeople(
            new Person { Id = personB, Name = "Person B" },
            new Person { Id = personC, Name = "Person C" });
        // A brand-new credit must go through TrackNew (see SyncCredits), not movie.MovieCredits.Add
        // directly — simulate EF's relationship fixup so the collection still reflects it afterwards,
        // the way a real DbContext would.
        _uowMock.Setup(u => u.MovieCreditStore.TrackNew(It.IsAny<MovieCredit>()))
            .Callback<MovieCredit>(c => movie.MovieCredits.Add(c));

        await _sut.UpdateAsync(new UpdateMovieRequest
        {
            Id = movieId, Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonId = personB, PersonName = "Person B", CreditType = CreditType.Director, DisplayOrder = 5 },
                new() { PersonId = personC, PersonName = "Person C", CreditType = CreditType.Actor, CharacterName = "Elphaba", DisplayOrder = 1 },
            },
        });

        _uowMock.Verify(u => u.MovieCreditStore.TrackNew(It.Is<MovieCredit>(c => c.PersonId == personC && c.CharacterName == "Elphaba")), Times.Once);
        movie.MovieCredits.Should().HaveCount(2);
        movie.MovieCredits.Should().NotContain(creditA);
        var keptB = movie.MovieCredits.Single(c => c.PersonId == personB);
        keptB.Should().BeSameAs(creditB, "an existing credit is updated in place, not replaced");
        keptB.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public async Task UpdateAsync_AddingFirstCredit_UsesTrackNew_NotCollectionAddDirectly()
    {
        // Regression test: BaseEntity.Id is assigned a non-default Guid in its property initializer,
        // so appending a brand-new MovieCredit straight to an already-tracked Movie's MovieCredits
        // collection makes EF Core's change tracker treat it as Modified instead of Added, producing
        // a no-op UPDATE and a DbUpdateConcurrencyException at SaveChanges. SyncCredits must route
        // every new row through IMovieCreditStore.TrackNew instead.
        var movieId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var movie = new Movie { Id = movieId, Title = "Wicked", Slug = "wicked", MovieTypeDetails = new List<MovieTypeDetail>(), MovieCredits = new List<MovieCredit>() };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        SetupPeople(new Person { Id = personId, Name = "Director Name" });

        await _sut.UpdateAsync(new UpdateMovieRequest
        {
            Id = movieId, Title = "Wicked",
            Credits = new List<MovieCreditRequest> { new() { PersonId = personId, PersonName = "Director Name", CreditType = CreditType.Director, DisplayOrder = 0 } },
        });

        _uowMock.Verify(u => u.MovieCreditStore.TrackNew(It.Is<MovieCredit>(c => c.PersonId == personId)), Times.Once);
        // Without a real DbContext the mocked TrackNew never actually appends to the collection, so an
        // empty collection here confirms the manager isn't also doing `movie.MovieCredits.Add(...)`.
        movie.MovieCredits.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_EmptyCredits_ClearsAll()
    {
        var movieId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = movieId, Title = "Wicked", Slug = "wicked",
            MovieTypeDetails = new List<MovieTypeDetail>(),
            MovieCredits = new List<MovieCredit> { new() { Id = Guid.NewGuid(), MovieId = movieId, PersonId = Guid.NewGuid(), CreditType = CreditType.Actor } },
        };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);

        await _sut.UpdateAsync(new UpdateMovieRequest { Id = movieId, Title = "Wicked", Credits = new List<MovieCreditRequest>() });

        movie.MovieCredits.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_DuplicatePersonAndRole_Throws()
    {
        var personId = Guid.NewGuid();
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());

        await _sut.Invoking(s => s.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonId = personId, PersonName = "Someone", CreditType = CreditType.Actor, DisplayOrder = 0 },
                new() { PersonId = personId, PersonName = "Someone", CreditType = CreditType.Actor, DisplayOrder = 1 },
            },
        })).Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()), Times.Never);
        // Caught purely in-memory, before the single batched PersonStore read.
        _uowMock.Verify(u => u.PersonStore.FindAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Person, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_UnknownPerson_Throws_AndDoesNotSave()
    {
        var movieId = Guid.NewGuid();
        var movie = new Movie { Id = movieId, Title = "Wicked", Slug = "wicked", MovieTypeDetails = new List<MovieTypeDetail>(), MovieCredits = new List<MovieCredit>() };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        SetupPeople();

        await _sut.Invoking(s => s.UpdateAsync(new UpdateMovieRequest
        {
            Id = movieId, Title = "Wicked",
            Credits = new List<MovieCreditRequest> { new() { PersonId = Guid.NewGuid(), PersonName = "Someone", CreditType = CreditType.Actor } },
        })).Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    // ── Inline Cast & Crew: match-or-create by name ─────────────────────────────

    [Fact]
    public async Task CreateAsync_PersonNameNoId_NoMatch_StagesNewPersonAndCreditReferencesIt()
    {
        Movie? created = null;
        Person? staged = null;
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        SetupPeople();
        _uowMock.Setup(u => u.PersonStore.TrackNew(It.IsAny<Person>())).Callback<Person>(p => staged = p);
        _uowMock.Setup(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()))
            .Callback<Movie>(m => created = m)
            .ReturnsAsync((Movie m) => m);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(It.IsAny<Guid>())).ReturnsAsync(() => created);

        await _sut.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonName = "  Cynthia Erivo ", PersonPhotoUrl = "  photo.jpg  ", CreditType = CreditType.Actor, CharacterName = "Elphaba" },
            },
        });

        staged.Should().NotBeNull();
        staged!.Name.Should().Be("Cynthia Erivo");
        staged.PhotoUrl.Should().Be("photo.jpg");
        created!.MovieCredits.Should().ContainSingle().Which.PersonId.Should().Be(staged.Id);
        _uowMock.Verify(u => u.PersonStore.CreateAsync(It.IsAny<Person>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_PersonNameNoId_MatchesExistingCaseInsensitively_ReusesAndUpdatesPhoto()
    {
        var existing = new Person { Id = Guid.NewGuid(), Name = "Cynthia Erivo", PhotoUrl = null };
        Movie? created = null;
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        SetupPeople(existing);
        _uowMock.Setup(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()))
            .Callback<Movie>(m => created = m)
            .ReturnsAsync((Movie m) => m);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(It.IsAny<Guid>())).ReturnsAsync(() => created);

        await _sut.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonName = " cynthia erivo ", PersonPhotoUrl = "new.jpg", CreditType = CreditType.Actor },
            },
        });

        _uowMock.Verify(u => u.PersonStore.TrackNew(It.IsAny<Person>()), Times.Never);
        created!.MovieCredits.Should().ContainSingle().Which.PersonId.Should().Be(existing.Id);
        existing.PhotoUrl.Should().Be("new.jpg", "a non-blank photo on a reused match replaces theirs");
    }

    [Fact]
    public async Task CreateAsync_PersonNameNoId_MatchesExisting_BlankPhoto_LeavesExistingPhotoUnchanged()
    {
        var existing = new Person { Id = Guid.NewGuid(), Name = "Cynthia Erivo", PhotoUrl = "original.jpg" };
        Movie? created = null;
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        SetupPeople(existing);
        _uowMock.Setup(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()))
            .Callback<Movie>(m => created = m)
            .ReturnsAsync((Movie m) => m);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(It.IsAny<Guid>())).ReturnsAsync(() => created);

        await _sut.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonName = "Cynthia Erivo", PersonPhotoUrl = "   ", CreditType = CreditType.Actor },
            },
        });

        existing.PhotoUrl.Should().Be("original.jpg");
    }

    [Fact]
    public async Task CreateAsync_BlankPersonName_Throws_AndStagesNothing()
    {
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());

        await _sut.Invoking(s => s.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest> { new() { PersonName = "   ", CreditType = CreditType.Actor } },
        })).Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()), Times.Never);
        _uowMock.Verify(u => u.PersonStore.TrackNew(It.IsAny<Person>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_SameNewNameTwiceInDifferentRoles_CreatesOnePerson()
    {
        Movie? created = null;
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        SetupPeople();
        _uowMock.Setup(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()))
            .Callback<Movie>(m => created = m)
            .ReturnsAsync((Movie m) => m);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(It.IsAny<Guid>())).ReturnsAsync(() => created);

        await _sut.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonName = "Jon M. Chu", CreditType = CreditType.Director },
                new() { PersonName = "JON M. CHU", CreditType = CreditType.Actor, CharacterName = "Cameo" },
            },
        });

        _uowMock.Verify(u => u.PersonStore.TrackNew(It.IsAny<Person>()), Times.Once);
        created!.MovieCredits.Should().HaveCount(2);
        created.MovieCredits.Select(c => c.PersonId).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_SameNewNameTwiceInSameRole_Throws_AndStagesNothing()
    {
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        SetupPeople();

        await _sut.Invoking(s => s.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonName = "Jon M. Chu", CreditType = CreditType.Director },
                new() { PersonName = "jon m. chu", CreditType = CreditType.Director },
            },
        })).Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.PersonStore.TrackNew(It.IsAny<Person>()), Times.Never);
        _uowMock.Verify(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ReusedPersonCollidesWithExplicitPick_SameRole_Throws_AndDoesNotStageOrSave()
    {
        var movieId = Guid.NewGuid();
        var existing = new Person { Id = Guid.NewGuid(), Name = "Cynthia Erivo" };
        var movie = new Movie { Id = movieId, Title = "Wicked", Slug = "wicked", MovieTypeDetails = new List<MovieTypeDetail>(), MovieCredits = new List<MovieCredit>() };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        SetupPeople(existing);

        await _sut.Invoking(s => s.UpdateAsync(new UpdateMovieRequest
        {
            Id = movieId, Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonId = existing.Id, PersonName = "Cynthia Erivo", CreditType = CreditType.Actor },
                new() { PersonName = "Cynthia Erivo", CreditType = CreditType.Actor },
            },
        })).Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.PersonStore.TrackNew(It.IsAny<Person>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    // ── Editing an existing person's name/photo via a movie save ────────────────

    [Fact]
    public async Task UpdateAsync_ExplicitPerson_EditedNameAndPhoto_UpdatesTrackedPerson()
    {
        var movieId = Guid.NewGuid();
        var person = new Person { Id = Guid.NewGuid(), Name = "Christopher Nolan", PhotoUrl = "old.jpg" };
        var movie = new Movie { Id = movieId, Title = "Wicked", Slug = "wicked", MovieTypeDetails = new List<MovieTypeDetail>(), MovieCredits = new List<MovieCredit>() };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        SetupPeople(person);
        _uowMock.Setup(u => u.MovieCreditStore.TrackNew(It.IsAny<MovieCredit>()))
            .Callback<MovieCredit>(c => movie.MovieCredits.Add(c));

        await _sut.UpdateAsync(new UpdateMovieRequest
        {
            Id = movieId, Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonId = person.Id, PersonName = "Chris Nolan", PersonPhotoUrl = "new.jpg", CreditType = CreditType.Director },
            },
        });

        person.Name.Should().Be("Chris Nolan");
        person.PhotoUrl.Should().Be("new.jpg");
        _uowMock.Verify(u => u.PersonStore.CreateAsync(It.IsAny<Person>()), Times.Never);
        _uowMock.Verify(u => u.PersonStore.UpdateAsync(It.IsAny<Person>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ExplicitPerson_RenameCollidesWithOtherPerson_Throws_AndDoesNotMutateOrSave()
    {
        var movieId = Guid.NewGuid();
        var p = new Person { Id = Guid.NewGuid(), Name = "Person P" };
        var q = new Person { Id = Guid.NewGuid(), Name = "Person Q" };
        var movie = new Movie { Id = movieId, Title = "Wicked", Slug = "wicked", MovieTypeDetails = new List<MovieTypeDetail>(), MovieCredits = new List<MovieCredit>() };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        SetupPeople(p, q);

        await _sut.Invoking(s => s.UpdateAsync(new UpdateMovieRequest
        {
            Id = movieId, Title = "Wicked",
            Credits = new List<MovieCreditRequest> { new() { PersonId = p.Id, PersonName = "person q", CreditType = CreditType.Director } },
        })).Should().ThrowAsync<InvalidOperationException>();

        p.Name.Should().Be("Person P");
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ExplicitPerson_CaseOnlyRename_Allowed()
    {
        var movieId = Guid.NewGuid();
        var person = new Person { Id = Guid.NewGuid(), Name = "christopher nolan" };
        var movie = new Movie { Id = movieId, Title = "Wicked", Slug = "wicked", MovieTypeDetails = new List<MovieTypeDetail>(), MovieCredits = new List<MovieCredit>() };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        SetupPeople(person);
        _uowMock.Setup(u => u.MovieCreditStore.TrackNew(It.IsAny<MovieCredit>()))
            .Callback<MovieCredit>(c => movie.MovieCredits.Add(c));

        await _sut.UpdateAsync(new UpdateMovieRequest
        {
            Id = movieId, Title = "Wicked",
            Credits = new List<MovieCreditRequest> { new() { PersonId = person.Id, PersonName = "Christopher Nolan", CreditType = CreditType.Director } },
        });

        person.Name.Should().Be("Christopher Nolan");
    }

    [Fact]
    public async Task UpdateAsync_ExplicitPerson_UnchangedValues_NoMutation()
    {
        var movieId = Guid.NewGuid();
        var fixedTime = new DateTime(2020, 1, 1);
        var person = new Person { Id = Guid.NewGuid(), Name = "Christopher Nolan", PhotoUrl = "photo.jpg", LastUpdatedTime = fixedTime };
        var movie = new Movie { Id = movieId, Title = "Wicked", Slug = "wicked", MovieTypeDetails = new List<MovieTypeDetail>(), MovieCredits = new List<MovieCredit>() };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        SetupPeople(person);
        _uowMock.Setup(u => u.MovieCreditStore.TrackNew(It.IsAny<MovieCredit>()))
            .Callback<MovieCredit>(c => movie.MovieCredits.Add(c));

        await _sut.UpdateAsync(new UpdateMovieRequest
        {
            Id = movieId, Title = "Wicked",
            Credits = new List<MovieCreditRequest> { new() { PersonId = person.Id, PersonName = "Christopher Nolan", PersonPhotoUrl = "photo.jpg", CreditType = CreditType.Director } },
        });

        person.LastUpdatedTime.Should().Be(fixedTime);
    }

    [Fact]
    public async Task UpdateAsync_ExplicitPerson_BlankPhoto_ClearsPhoto()
    {
        var movieId = Guid.NewGuid();
        var person = new Person { Id = Guid.NewGuid(), Name = "Christopher Nolan", PhotoUrl = "photo.jpg" };
        var movie = new Movie { Id = movieId, Title = "Wicked", Slug = "wicked", MovieTypeDetails = new List<MovieTypeDetail>(), MovieCredits = new List<MovieCredit>() };
        _uowMock.Setup(u => u.MovieStore.GetForUpdateAsync(movieId)).ReturnsAsync(movie);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(movieId)).ReturnsAsync(movie);
        SetupPeople(person);
        _uowMock.Setup(u => u.MovieCreditStore.TrackNew(It.IsAny<MovieCredit>()))
            .Callback<MovieCredit>(c => movie.MovieCredits.Add(c));

        await _sut.UpdateAsync(new UpdateMovieRequest
        {
            Id = movieId, Title = "Wicked",
            Credits = new List<MovieCreditRequest> { new() { PersonId = person.Id, PersonName = "Christopher Nolan", PersonPhotoUrl = "   ", CreditType = CreditType.Director } },
        });

        person.PhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_SamePersonTwoRows_ConflictingEdits_Throws()
    {
        var personId = Guid.NewGuid();
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        SetupPeople(new Person { Id = personId, Name = "Christopher Nolan" });

        await _sut.Invoking(s => s.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonId = personId, PersonName = "Christopher Nolan", PersonPhotoUrl = "a.jpg", CreditType = CreditType.Director },
                new() { PersonId = personId, PersonName = "Chris Nolan", PersonPhotoUrl = "b.jpg", CreditType = CreditType.Actor },
            },
        })).Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_NewRowNameMatchesRenameTarget_ReusesRenamedPerson()
    {
        var personId = Guid.NewGuid();
        Movie? created = null;
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        SetupPeople(new Person { Id = personId, Name = "Christopher Nolan" });
        _uowMock.Setup(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()))
            .Callback<Movie>(m => created = m)
            .ReturnsAsync((Movie m) => m);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(It.IsAny<Guid>())).ReturnsAsync(() => created);

        await _sut.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonId = personId, PersonName = "Chris Nolan", CreditType = CreditType.Director },
                new() { PersonName = "Chris Nolan", CreditType = CreditType.Actor },
            },
        });

        _uowMock.Verify(u => u.PersonStore.TrackNew(It.IsAny<Person>()), Times.Never);
        created!.MovieCredits.Select(c => c.PersonId).Distinct().Should().ContainSingle().Which.Should().Be(personId);
    }

    [Fact]
    public async Task CreateAsync_NewRowNameMatchesNameBeingRenamedAway_Throws()
    {
        var personId = Guid.NewGuid();
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        SetupPeople(new Person { Id = personId, Name = "Christopher Nolan" });

        await _sut.Invoking(s => s.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonId = personId, PersonName = "Chris Nolan", CreditType = CreditType.Director },
                new() { PersonName = "Christopher Nolan", CreditType = CreditType.Actor },
            },
        })).Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_TwoRenamesToSameName_Throws()
    {
        var personA = Guid.NewGuid();
        var personB = Guid.NewGuid();
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        SetupPeople(
            new Person { Id = personA, Name = "Person A" },
            new Person { Id = personB, Name = "Person B" });

        await _sut.Invoking(s => s.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonId = personA, PersonName = "New Name", CreditType = CreditType.Director },
                new() { PersonId = personB, PersonName = "new name", CreditType = CreditType.Actor },
            },
        })).Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.MovieStore.CreateAsync(It.IsAny<Movie>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_OnlyOnePersonQuery_ForManyRows()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        _uowMock.Setup(u => u.MovieStore.GetSlugsStartingWithAsync("wicked")).ReturnsAsync(new List<string>());
        SetupPeople(
            new Person { Id = p1, Name = "Person One" },
            new Person { Id = p2, Name = "Person Two" });
        _uowMock.Setup(u => u.MovieStore.CreateAsync(It.IsAny<Movie>())).ReturnsAsync((Movie m) => m);
        _uowMock.Setup(u => u.MovieStore.GetDetailAsync(It.IsAny<Guid>())).ReturnsAsync(new Movie { Id = Guid.NewGuid() });

        await _sut.CreateAsync(new CreateMovieRequest
        {
            Title = "Wicked",
            Credits = new List<MovieCreditRequest>
            {
                new() { PersonId = p1, PersonName = "Person One", CreditType = CreditType.Director },
                new() { PersonId = p2, PersonName = "Person Two", CreditType = CreditType.Actor },
                new() { PersonName = "Brand New One", CreditType = CreditType.Director, DisplayOrder = 1 },
                new() { PersonName = "Brand New Two", CreditType = CreditType.Actor, DisplayOrder = 2 },
            },
        });

        _uowMock.Verify(u => u.PersonStore.FindAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Person, bool>>>()), Times.Once);
    }
}
