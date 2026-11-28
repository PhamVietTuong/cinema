using Cinema.Data.Stores;
using FluentAssertions;

namespace Cinema.Business.Tests;

public class QueryablePagingExtensionsTests
{
    private static IQueryable<int> Numbers(int count) => Enumerable.Range(1, count).AsQueryable();

    [Theory]
    [InlineData(1, 10, 1, 10)]
    [InlineData(2, 10, 11, 20)]
    [InlineData(3, 10, 21, 25)]
    public void PageOneBased_FirstPageIsOne(int page, int pageSize, int first, int last)
    {
        var rows = Numbers(25).PageOneBased(page, pageSize).ToList();

        rows.First().Should().Be(first);
        rows.Last().Should().Be(last);
    }

    [Theory]
    [InlineData(0, 10, 1, 10)]
    [InlineData(1, 10, 11, 20)]
    [InlineData(2, 10, 21, 25)]
    public void PageZeroBased_FirstPageIsZero(int pageIndex, int pageSize, int first, int last)
    {
        var rows = Numbers(25).PageZeroBased(pageIndex, pageSize).ToList();

        rows.First().Should().Be(first);
        rows.Last().Should().Be(last);
    }

    [Fact]
    public void PastTheLastPage_ReturnsNoRows()
    {
        Numbers(25).PageOneBased(4, 10).Should().BeEmpty();
        Numbers(25).PageZeroBased(3, 10).Should().BeEmpty();
    }

    [Fact]
    public void OneBasedPageN_EqualsZeroBasedPageNMinusOne()
    {
        Numbers(95).PageOneBased(5, 7).Should().Equal(Numbers(95).PageZeroBased(4, 7));
    }
}
