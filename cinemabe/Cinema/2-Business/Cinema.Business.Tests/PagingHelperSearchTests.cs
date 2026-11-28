using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class PagingHelperSearchTests
{
    private readonly Mock<IGenericStore<SeatType>> _store = new();
    private readonly List<SeatType> _rows = new() { new() { Name = "A" }, new() { Name = "B" } };

    private IQueryable<SeatType> Filter(Dictionary<string, string>? filters)
    {
        LastFilters = filters;
        return _rows.AsQueryable();
    }

    private IQueryable<SeatType> Sort(IQueryable<SeatType> query, SortDTO? sort)
    {
        LastSort = sort;
        return query;
    }

    private Dictionary<string, string>? LastFilters { get; set; }
    private SortDTO? LastSort { get; set; }

    public PagingHelperSearchTests()
    {
        _store.Setup(s => s.CountAsync(It.IsAny<IQueryable<SeatType>>())).ReturnsAsync(42);
        _store.Setup(s => s.AllPageAsync(It.IsAny<IQueryable<SeatType>>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(_rows);
    }

    [Fact]
    public async Task NullRequest_UsesDefaultsAndReadsTheFirstPage()
    {
        var result = await PagingHelper.SearchAsync<SeatType, SeatTypeDTO>(_store.Object, null, Filter, Sort);

        _store.Verify(s => s.AllPageAsync(It.IsAny<IQueryable<SeatType>>(), 0, 20), Times.Once);
        result.Page.Should().Be(1);
        result.CountPerPage.Should().Be(20);
        result.TotalCount.Should().Be(42);
        result.Results.Select(r => r.Name).Should().Equal("A", "B");
    }

    [Fact]
    public async Task RequestPage_IsOneBasedForTheCallerAndZeroBasedForTheStore()
    {
        var search = new PagingSearchDTO { PageIndex = 3, PageSize = 15 };

        var result = await PagingHelper.SearchAsync<SeatType, SeatTypeDTO>(_store.Object, search, Filter, Sort);

        _store.Verify(s => s.AllPageAsync(It.IsAny<IQueryable<SeatType>>(), 2, 15), Times.Once);
        result.Page.Should().Be(3);
        result.CountPerPage.Should().Be(15);
    }

    [Fact]
    public async Task FilterAndSortReceiveTheRequestParts()
    {
        var filters = new Dictionary<string, string> { [FilterKeys.Keyword] = "vip" };
        var sort = new SortDTO { Field = "name", Ascending = false };

        await PagingHelper.SearchAsync<SeatType, SeatTypeDTO>(_store.Object, new PagingSearchDTO { Filters = filters, Sort = sort }, Filter, Sort);

        LastFilters.Should().BeSameAs(filters);
        LastSort.Should().BeSameAs(sort);
    }
}
