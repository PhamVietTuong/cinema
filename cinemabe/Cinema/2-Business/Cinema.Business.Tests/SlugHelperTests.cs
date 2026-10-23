using Cinema.Business.Helpers;
using FluentAssertions;

namespace Cinema.Business.Tests;

public class SlugHelperTests
{
    [Theory]
    [InlineData("Avengers: Endgame", "avengers-endgame")]
    [InlineData("Lật Mặt 7: Một Điều Ước", "lat-mat-7-mot-dieu-uoc")]
    [InlineData("Đất Rừng Phương Nam", "dat-rung-phuong-nam")]
    [InlineData("  --Spider-Man   2!! ", "spider-man-2")]
    [InlineData("Fast & Furious 10", "fast-furious-10")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("!!!", "")]
    [InlineData("千と千尋", "")]
    public void Slugify_ProducesExpectedSlug(string? input, string expected)
    {
        SlugHelper.Slugify(input).Should().Be(expected);
    }

    [Fact]
    public void ToBaseSlug_EmptyTitle_FallsBackToMovie()
    {
        SlugHelper.ToBaseSlug("千と千尋").Should().Be("movie");
    }

    [Fact]
    public void ToBaseSlug_GuidShapedTitle_PrefixedWithMovie()
    {
        var guid = Guid.NewGuid().ToString();

        SlugHelper.ToBaseSlug(guid).Should().Be($"movie-{guid}");
    }

    [Fact]
    public void ToBaseSlug_LongTitle_TruncatesTo300()
    {
        var title = new string('a', 400);

        var slug = SlugHelper.ToBaseSlug(title);

        slug.Length.Should().BeLessOrEqualTo(SlugHelper.MaxBaseLength);
    }

    [Fact]
    public void PickUnique_FreeBase_ReturnsBase()
    {
        SlugHelper.PickUnique("wicked", Array.Empty<string>()).Should().Be("wicked");
    }

    [Fact]
    public void PickUnique_BaseTaken_ReturnsSuffix2()
    {
        SlugHelper.PickUnique("wicked", new[] { "wicked" }).Should().Be("wicked-2");
    }

    [Fact]
    public void PickUnique_BaseAndSuffix2Taken_ReturnsSuffix3()
    {
        SlugHelper.PickUnique("wicked", new[] { "wicked", "wicked-2" }).Should().Be("wicked-3");
    }

    [Fact]
    public void PickUnique_UnrelatedSlugWithSameBasePrefix_DoesNotCountAsTaken()
    {
        SlugHelper.PickUnique("wicked", new[] { "wicked-for-good" }).Should().Be("wicked");
    }
}
