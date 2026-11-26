using System.Linq.Expressions;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

/// <summary>Drafts must never reach a caller who is not an admin, whatever filter they send.</summary>
public class NewsManagerTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly NewsManager _sut;
    private readonly News _published = new() { Id = Guid.NewGuid(), Title = "Out now", IsPublished = true };
    private readonly News _draft = new() { Id = Guid.NewGuid(), Title = "Secret draft", IsPublished = false };

    public NewsManagerTests()
    {
        var all = new List<News> { _published, _draft };
        _uowMock.Setup(u => u.NewsStore.GetQuery()).Returns(() => all.AsQueryable());
        _uowMock.Setup(u => u.NewsStore.FilterQuery(It.IsAny<IQueryable<News>>(), It.IsAny<Expression<Func<News, bool>>>()))
            .Returns((IQueryable<News> q, Expression<Func<News, bool>> e) => q.Where(e));
        _uowMock.Setup(u => u.NewsStore.CountAsync(It.IsAny<IQueryable<News>>()))
            .ReturnsAsync((IQueryable<News> q) => q.Count());
        _uowMock.Setup(u => u.NewsStore.AllPageAsync(It.IsAny<IQueryable<News>>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((IQueryable<News> q, int _, int _) => q.ToList());
        _uowMock.Setup(u => u.NewsStore.GetByIdAsync(_published.Id)).ReturnsAsync(_published);
        _uowMock.Setup(u => u.NewsStore.GetByIdAsync(_draft.Id)).ReturnsAsync(_draft);

        _sut = new NewsManager(_uowMock.Object);
    }

    [Fact]
    public async Task GetAsync_PublishedOnly_HidesDraftsEvenWhenTheFilterAsksForThem()
    {
        var search = new PagingSearchDTO { Filters = new Dictionary<string, string> { ["isPublished"] = "false" } };

        var result = await _sut.GetAsync(search, publishedOnly: true);

        result.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_PublishedOnly_WithNoFilter_ReturnsOnlyPublished()
    {
        var result = await _sut.GetAsync(new PagingSearchDTO(), publishedOnly: true);

        result.Results.Should().ContainSingle().Which.Title.Should().Be("Out now");
    }

    [Fact]
    public async Task GetAsync_Admin_SeesDraftsToo()
    {
        var result = await _sut.GetAsync(new PagingSearchDTO());

        result.Results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_PublishedOnly_ReportsADraftAsNotFound()
    {
        await _sut.Invoking(s => s.GetByIdAsync(_draft.Id, publishedOnly: true))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_PublishedOnly_ReturnsAPublishedArticle()
    {
        var dto = await _sut.GetByIdAsync(_published.Id, publishedOnly: true);

        dto.Title.Should().Be("Out now");
    }

    [Fact]
    public async Task GetByIdAsync_Admin_CanReadADraft()
    {
        var dto = await _sut.GetByIdAsync(_draft.Id);

        dto.Title.Should().Be("Secret draft");
    }
}
