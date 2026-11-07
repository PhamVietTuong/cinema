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
        // Default: the user has not rated anything.
        _uowMock.Setup(u => u.EvaluationStore.GetUserEvaluationAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(((int Score, string? Review)?)null);
        // Default: no external ratings, so existing GetDetailAsync tests don't need to stub this.
        _externalRatingsMock.Setup(p => p.GetRatingsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalRatingsDTO?)null);
    }

    [Fact]
    public async Task GetMoviesAsync_ReturnsMappedResults()
    {
        var movieId = Guid.NewGuid();
        var movies  = new List<Movie> { new() { Id = movieId, Title = "Movie A" } };
        _uowMock.Setup(u => u.MovieStore.GetPagedAsync(It.IsAny<MovieSearchFilter>(), 1, 12)).ReturnsAsync((movies, 1));
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
        _uowMock.Setup(u => u.EvaluationStore.TryAddAsync(It.IsAny<Evaluation>())).ReturnsAsync(true);

        var score = await _sut.RateMovieAsync(movieId, userId, 5, "Great");

        score.Should().Be(5);
        _uowMock.Verify(u => u.EvaluationStore.TryAddAsync(It.Is<Evaluation>(e => e.Score == 5 && e.Review == "Great")), Times.Once);
    }

    private void SetupWatched(Guid movieId, Guid userId, bool watched = true)
    {
        _uowMock.Setup(u => u.InvoiceStore.HasWatchedMovieAsync(userId, movieId, It.IsAny<DateTime>())).ReturnsAsync(watched);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-3)]
    public async Task RateMovieAsync_ScoreOutsideOneToTen_Throws_AndDoesNotWrite(int score)
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        SetupWatched(movieId, userId);

        await _sut.Invoking(s => s.RateMovieAsync(movieId, userId, score, null))
            .Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.EvaluationStore.TryAddAsync(It.IsAny<Evaluation>()), Times.Never);
        _uowMock.Verify(u => u.EvaluationStore.UpdateAsync(It.IsAny<Evaluation>()), Times.Never);
    }

    [Fact]
    public async Task RateMovieAsync_StarClickWithoutReview_PreservesSavedReview()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        SetupWatched(movieId, userId);
        var existing = new Evaluation { MovieId = movieId, UserId = userId, Score = 3, Review = "Loved the score" };
        _uowMock.Setup(u => u.EvaluationStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Evaluation, bool>>>()))
            .ReturnsAsync(new List<Evaluation> { existing });

        await _sut.RateMovieAsync(movieId, userId, 9, null);

        existing.Score.Should().Be(9);
        existing.Review.Should().Be("Loved the score");
        _uowMock.Verify(u => u.EvaluationStore.UpdateAsync(existing), Times.Once);
    }

    [Fact]
    public async Task RateMovieAsync_BlankReview_ClearsSavedReview()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        SetupWatched(movieId, userId);
        var existing = new Evaluation { MovieId = movieId, UserId = userId, Score = 3, Review = "Old" };
        _uowMock.Setup(u => u.EvaluationStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Evaluation, bool>>>()))
            .ReturnsAsync(new List<Evaluation> { existing });

        await _sut.RateMovieAsync(movieId, userId, 4, "   ");

        existing.Review.Should().BeNull();
    }

    [Fact]
    public async Task RateMovieAsync_ConcurrentFirstRating_BecomesUpdateOfWinningRow()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        SetupWatched(movieId, userId);
        var winner = new Evaluation { MovieId = movieId, UserId = userId, Score = 2 };
        _uowMock.SetupSequence(u => u.EvaluationStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Evaluation, bool>>>()))
            .ReturnsAsync(new List<Evaluation>())
            .ReturnsAsync(new List<Evaluation> { winner });
        _uowMock.Setup(u => u.EvaluationStore.TryAddAsync(It.IsAny<Evaluation>())).ReturnsAsync(false);

        await _sut.RateMovieAsync(movieId, userId, 8, null);

        winner.Score.Should().Be(8);
        _uowMock.Verify(u => u.EvaluationStore.UpdateAsync(winner), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddCommentAsync_Blank_Throws(string content)
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        SetupWatched(movieId, userId);

        await _sut.Invoking(s => s.AddCommentAsync(movieId, userId, content, null))
            .Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.CommentStore.CreateAsync(It.IsAny<Comment>()), Times.Never);
    }

    [Fact]
    public async Task AddCommentAsync_Over2000Chars_Throws()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        SetupWatched(movieId, userId);

        await _sut.Invoking(s => s.AddCommentAsync(movieId, userId, new string('x', 2001), null))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AddCommentAsync_Exactly2000Chars_Succeeds()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        SetupWatched(movieId, userId);
        _uowMock.Setup(u => u.CommentStore.CreateAsync(It.IsAny<Comment>())).ReturnsAsync((Comment c) => c);

        var result = await _sut.AddCommentAsync(movieId, userId, new string('x', 2000), null);

        result.Content.Should().HaveLength(2000);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("otherMovie")]
    [InlineData("unapproved")]
    [InlineData("isReply")]
    public async Task AddCommentAsync_InvalidReplyParent_Throws(string scenario)
    {
        var movieId  = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        SetupWatched(movieId, userId);
        Comment? parent = scenario switch
        {
            "missing"    => null,
            "otherMovie" => new Comment { Id = parentId, MovieId = Guid.NewGuid() },
            "unapproved" => new Comment { Id = parentId, MovieId = movieId, IsApproved = false },
            _            => new Comment { Id = parentId, MovieId = movieId, ParentId = Guid.NewGuid() },
        };
        _uowMock.Setup(u => u.CommentStore.GetByIdAsync(parentId)).ReturnsAsync(parent);

        await _sut.Invoking(s => s.AddCommentAsync(movieId, userId, "reply", parentId))
            .Should().ThrowAsync<InvalidOperationException>();

        _uowMock.Verify(u => u.CommentStore.CreateAsync(It.IsAny<Comment>()), Times.Never);
    }

    [Fact]
    public async Task AddCommentAsync_ValidReply_Creates()
    {
        var movieId  = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        SetupWatched(movieId, userId);
        _uowMock.Setup(u => u.CommentStore.GetByIdAsync(parentId)).ReturnsAsync(new Comment { Id = parentId, MovieId = movieId });
        _uowMock.Setup(u => u.CommentStore.CreateAsync(It.IsAny<Comment>())).ReturnsAsync((Comment c) => c);

        var result = await _sut.AddCommentAsync(movieId, userId, "  me too ", parentId);

        result.Content.Should().Be("me too");
        result.ParentId.Should().Be(parentId);
    }

    [Fact]
    public async Task UpdateOwnCommentAsync_Author_UpdatesContent()
    {
        var userId  = Guid.NewGuid();
        var comment = new Comment { Id = Guid.NewGuid(), UserId = userId, Content = "old" };
        _uowMock.Setup(u => u.CommentStore.GetByIdAsync(comment.Id)).ReturnsAsync(comment);

        var result = await _sut.UpdateOwnCommentAsync(comment.Id, userId, " new text ");

        comment.Content.Should().Be("new text");
        result.IsMine.Should().BeTrue();
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateOwnCommentAsync_NonAuthor_ThrowsAccessDenied()
    {
        var comment = new Comment { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Content = "old" };
        _uowMock.Setup(u => u.CommentStore.GetByIdAsync(comment.Id)).ReturnsAsync(comment);

        await _sut.Invoking(s => s.UpdateOwnCommentAsync(comment.Id, Guid.NewGuid(), "hack"))
            .Should().ThrowAsync<AccessDeniedException>();

        comment.Content.Should().Be("old");
    }

    [Fact]
    public async Task DeleteOwnCommentAsync_Author_RemovesCommentAndReplies()
    {
        var userId  = Guid.NewGuid();
        var comment = new Comment { Id = Guid.NewGuid(), UserId = userId };
        var reply   = new Comment { Id = Guid.NewGuid(), ParentId = comment.Id };
        _uowMock.Setup(u => u.CommentStore.GetByIdAsync(comment.Id)).ReturnsAsync(comment);
        _uowMock.Setup(u => u.CommentStore.GetRepliesAsync(comment.Id)).ReturnsAsync(new List<Comment> { reply });

        await _sut.DeleteOwnCommentAsync(comment.Id, userId);

        _uowMock.Verify(u => u.CommentStore.DeleteAsync(reply), Times.Once);
        _uowMock.Verify(u => u.CommentStore.DeleteAsync(comment), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteOwnCommentAsync_NonAuthor_ThrowsAccessDenied_AndDeletesNothing()
    {
        var comment = new Comment { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        _uowMock.Setup(u => u.CommentStore.GetByIdAsync(comment.Id)).ReturnsAsync(comment);

        await _sut.Invoking(s => s.DeleteOwnCommentAsync(comment.Id, Guid.NewGuid()))
            .Should().ThrowAsync<AccessDeniedException>();

        _uowMock.Verify(u => u.CommentStore.DeleteAsync(It.IsAny<Comment>()), Times.Never);
    }

    [Fact]
    public async Task GetDetailAsync_SignedInUser_ReturnsMyRatingMyReviewAndOwnership()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var movie   = new Movie { Id = movieId, Title = "Test Movie" };
        SetupBasicDetailRead(movieId, movie);
        SetupWatched(movieId, userId);
        _uowMock.Setup(u => u.MovieStore.GetRatingCountAsync(movieId)).ReturnsAsync(3);
        _uowMock.Setup(u => u.CommentStore.CountApprovedForMovieAsync(movieId)).ReturnsAsync(5);
        _uowMock.Setup(u => u.EvaluationStore.GetUserEvaluationAsync(movieId, userId)).ReturnsAsync((7, "Solid"));
        _uowMock.Setup(u => u.CommentStore.GetRecentForMovieAsync(movieId, 10)).ReturnsAsync(new List<CommentView>
        {
            new(Guid.NewGuid(), "mine", null, userId, DateTime.UtcNow, "Me", null, new List<CommentView>()),
            new(Guid.NewGuid(), "theirs", null, Guid.NewGuid(), DateTime.UtcNow, "Other", null, new List<CommentView>()),
        });

        var result = await _sut.GetDetailAsync(movieId, userId);

        result.MyRating.Should().Be(7);
        result.MyReview.Should().Be("Solid");
        result.RatingCount.Should().Be(3);
        result.TotalComments.Should().Be(5);
        result.RecentComments.Select(c => c.IsMine).Should().Equal(true, false);
    }

    [Fact]
    public async Task GetDetailAsync_NoRatings_RatingCountZero_MyRatingNull()
    {
        var movieId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        SetupBasicDetailRead(movieId, new Movie { Id = movieId, Title = "Test Movie" });
        SetupWatched(movieId, userId, false);

        var result = await _sut.GetDetailAsync(movieId, userId);

        result.RatingCount.Should().Be(0);
        result.MyRating.Should().BeNull();
    }

    [Fact]
    public async Task GetMoviesAsync_MapsFiltersToStoreFilter()
    {
        MovieSearchFilter? captured = null;
        _uowMock.Setup(u => u.MovieStore.GetPagedAsync(It.IsAny<MovieSearchFilter>(), 2, 15))
            .Callback<MovieSearchFilter, int, int>((f, _, _) => captured = f)
            .ReturnsAsync((new List<Movie>(), 0));
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        var typeId = Guid.NewGuid();
        var theaterId = Guid.NewGuid();
        var future = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

        await _sut.GetMoviesAsync(new PagingSearchDTO
        {
            PageIndex = 2, PageSize = 15,
            Filters = new() { ["movieTypeId"] = typeId.ToString(), ["language"] = "English", ["theaterId"] = theaterId.ToString(), ["date"] = future.ToString("yyyy-MM-dd") },
        });

        captured.Should().NotBeNull();
        captured!.MovieTypeId.Should().Be(typeId);
        captured.Language.Should().Be("English");
        captured.TheaterId.Should().Be(theaterId);
        captured.ShowFrom.Should().Be(future.ToDateTime(TimeOnly.MinValue));
        captured.ShowTo.Should().Be(future.ToDateTime(TimeOnly.MinValue).AddDays(1));
    }

    [Fact]
    public async Task SearchShowTimesAsync_MapsTimeRange_AndRows()
    {
        ShowTimeSearchFilter? captured = null;
        var row = new ShowTimeSearchRow(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 12, 1, 19, 30, 0), new DateTime(2026, 12, 1, 21, 30, 0),
            ProjectionForm.TwoD, Guid.NewGuid(), "Wicked", "wicked", null, "English", Guid.NewGuid(), "Cinema One", "Room 1", "Standard", 90000);
        _uowMock.Setup(u => u.ShowTimeStore.SearchRoomsAsync(It.IsAny<ShowTimeSearchFilter>(), 1, 20))
            .Callback<ShowTimeSearchFilter, int, int>((f, _, _) => captured = f)
            .ReturnsAsync((new List<ShowTimeSearchRow> { row }, 1));

        var result = await _sut.SearchShowTimesAsync(new PagingSearchDTO
        {
            Filters = new() { ["timeFrom"] = "18:00", ["timeTo"] = "22:00", ["date"] = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd") },
        });

        captured!.TimeFrom.Should().Be(new TimeOnly(18, 0));
        captured.TimeTo.Should().Be(new TimeOnly(22, 0));
        result.TotalCount.Should().Be(1);
        result.Results.Single().MovieTitle.Should().Be("Wicked");
    }

    private async Task<MovieSearchFilter> CaptureMovieFilterAsync(Dictionary<string, string> filters)
    {
        MovieSearchFilter? captured = null;
        _uowMock.Setup(u => u.MovieStore.GetPagedAsync(It.IsAny<MovieSearchFilter>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<MovieSearchFilter, int, int>((f, _, _) => captured = f)
            .ReturnsAsync((new List<Movie>(), 0));
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        await _sut.GetMoviesAsync(new PagingSearchDTO { Filters = filters });
        return captured!;
    }

    [Theory]
    [InlineData("now", "now")]
    [InlineData("coming", "coming")]
    [InlineData("bogus", null)]
    public async Task GetMoviesAsync_Status_OnlyNowOrComingReachesFilter(string status, string? expected)
    {
        var filter = await CaptureMovieFilterAsync(new() { ["status"] = status });

        filter.Status.Should().Be(expected);
    }

    [Fact]
    public async Task GetMoviesAsync_TheaterOnly_ShowFromNowAndNoUpperBound()
    {
        var filter = await CaptureMovieFilterAsync(new() { ["theaterId"] = Guid.NewGuid().ToString() });

        filter.ShowFrom.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        filter.ShowTo.Should().BeNull();
    }

    [Fact]
    public async Task GetMoviesAsync_DateToday_ClampsShowFromToNow()
    {
        var filter = await CaptureMovieFilterAsync(new() { ["date"] = DateTime.Today.ToString("yyyy-MM-dd") });

        filter.ShowFrom.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        filter.ShowTo.Should().Be(DateTime.Today.AddDays(1));
    }

    [Fact]
    public async Task GetMoviesAsync_PastDate_ClampsShowFromToNow_SoNothingMatches()
    {
        var filter = await CaptureMovieFilterAsync(new() { ["date"] = DateTime.Today.AddDays(-5).ToString("yyyy-MM-dd") });

        filter.ShowFrom.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        filter.ShowTo.Should().BeBefore(filter.ShowFrom!.Value);
    }

    private async Task<(ShowTimeSearchFilter Filter, int PageSize)> CaptureShowTimeFilterAsync(Dictionary<string, string>? filters, int pageSize = 20)
    {
        ShowTimeSearchFilter? captured = null;
        var capturedSize = 0;
        _uowMock.Setup(u => u.ShowTimeStore.SearchRoomsAsync(It.IsAny<ShowTimeSearchFilter>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<ShowTimeSearchFilter, int, int>((f, _, size) => { captured = f; capturedSize = size; })
            .ReturnsAsync((new List<ShowTimeSearchRow>(), 0));
        await _sut.SearchShowTimesAsync(new PagingSearchDTO { PageSize = pageSize, Filters = filters });
        return (captured!, capturedSize);
    }

    [Fact]
    public async Task SearchShowTimesAsync_FutureDate_CoversThatWholeDay()
    {
        var day = DateTime.Today.AddDays(10);

        var (filter, _) = await CaptureShowTimeFilterAsync(new() { ["date"] = day.ToString("yyyy-MM-dd") });

        filter.From.Should().Be(day);
        filter.To.Should().Be(day.AddDays(1));
    }

    [Fact]
    public async Task SearchShowTimesAsync_NoDate_StartsNowWithNoUpperBound()
    {
        var (filter, _) = await CaptureShowTimeFilterAsync(null);

        filter.From.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        filter.To.Should().BeNull();
    }

    [Fact]
    public async Task SearchShowTimesAsync_Today_ClampsFromToNow()
    {
        var (filter, _) = await CaptureShowTimeFilterAsync(new() { ["date"] = DateTime.Today.ToString("yyyy-MM-dd") });

        filter.From.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        filter.To.Should().Be(DateTime.Today.AddDays(1));
    }

    [Fact]
    public async Task SearchShowTimesAsync_PastDate_ClampsFromToNow()
    {
        var (filter, _) = await CaptureShowTimeFilterAsync(new() { ["date"] = DateTime.Today.AddDays(-3).ToString("yyyy-MM-dd") });

        filter.From.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        filter.To.Should().BeBefore(filter.From!.Value);
    }

    [Fact]
    public async Task SearchShowTimesAsync_PageSizeCappedAt100()
    {
        var (_, size) = await CaptureShowTimeFilterAsync(null, pageSize: 5000);

        size.Should().Be(100);
    }

    [Fact]
    public async Task GetCommentsAsync_SetsIsMineOnRepliesToo_AndNotForAnonymous()
    {
        var movieId = Guid.NewGuid();
        var me = Guid.NewGuid();
        var reply = new CommentView(Guid.NewGuid(), "my reply", Guid.NewGuid(), me, DateTime.UtcNow, "Me", null, new List<CommentView>());
        var top = new CommentView(Guid.NewGuid(), "theirs", null, Guid.NewGuid(), DateTime.UtcNow, "Other", null, new List<CommentView> { reply });
        _uowMock.Setup(u => u.CommentStore.GetPagedForMovieAsync(movieId, 1, 10))
            .ReturnsAsync((new List<CommentView> { top }, 1));

        var signedIn = await _sut.GetCommentsAsync(movieId, me, new PagingSearchDTO { PageSize = 10 });
        var anonymous = await _sut.GetCommentsAsync(movieId, null, new PagingSearchDTO { PageSize = 10 });

        signedIn.Results.Single().IsMine.Should().BeFalse();
        signedIn.Results.Single().Replies.Single().IsMine.Should().BeTrue();
        anonymous.Results.Single().Replies.Single().IsMine.Should().BeFalse();
    }

    [Fact]
    public async Task GetCommentsAsync_PageSizeCappedAt50()
    {
        var movieId = Guid.NewGuid();
        _uowMock.Setup(u => u.CommentStore.GetPagedForMovieAsync(movieId, 2, 50))
            .ReturnsAsync((new List<CommentView>(), 0));

        var result = await _sut.GetCommentsAsync(movieId, null, new PagingSearchDTO { PageIndex = 2, PageSize = 500 });

        result.CountPerPage.Should().Be(50);
        _uowMock.Verify(u => u.CommentStore.GetPagedForMovieAsync(movieId, 2, 50), Times.Once);
    }

    [Fact]
    public async Task SearchShowTimesAsync_InvertedTimeRange_Throws()
    {
        await _sut.Invoking(s => s.SearchShowTimesAsync(new PagingSearchDTO
            {
                Filters = new() { ["timeFrom"] = "22:00", ["timeTo"] = "18:00" },
            }))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetRecommendedAsync_TreatsScoresFromSevenAsLiked()
    {
        var userId  = Guid.NewGuid();
        var liked   = new Movie { Id = Guid.NewGuid(), Title = "Liked", IsActive = true, MovieTypeDetails = new List<MovieTypeDetail> { new() { MovieType = new MovieType { Name = "Horror" } } } };
        var horror  = new Movie { Id = Guid.NewGuid(), Title = "Horror 2", IsActive = true, MovieTypeDetails = new List<MovieTypeDetail> { new() { MovieType = new MovieType { Name = "Horror" } } } };
        var comedy  = new Movie { Id = Guid.NewGuid(), Title = "Comedy", IsActive = true, MovieTypeDetails = new List<MovieTypeDetail> { new() { MovieType = new MovieType { Name = "Comedy" } } } };
        _uowMock.Setup(u => u.MovieStore.GetPagedAsync(It.IsAny<MovieSearchFilter>(), 1, 200)).ReturnsAsync((new List<Movie> { comedy, horror, liked }, 3));
        _uowMock.Setup(u => u.MovieStore.GetAverageRatingsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, double> { [comedy.Id] = 9, [horror.Id] = 5 });
        _uowMock.Setup(u => u.EvaluationStore.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Evaluation, bool>>>()))
            .ReturnsAsync(new List<Evaluation> { new() { MovieId = liked.Id, UserId = userId, Score = 7 } });

        var result = await _sut.GetRecommendedAsync(userId, 5);

        result.Select(m => m.Title).Should().Equal("Horror 2", "Comedy");
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
        _uowMock.Setup(u => u.MovieStore.GetPagedAsync(It.IsAny<MovieSearchFilter>(), 1, 12)).ReturnsAsync((movies, 1));
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
