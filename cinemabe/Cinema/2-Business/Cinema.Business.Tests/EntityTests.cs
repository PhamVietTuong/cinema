using Cinema.Business.DTO.Movies;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;

namespace Cinema.Business.Tests;

public class EntityTests
{
    [Fact]
    public void User_DefaultStatus_IsActive()
    {
        var user = new User();
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void User_NewGuid_IsNotEmpty()
    {
        var user = new User();
        user.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Movie_DefaultIsActive_IsTrue()
    {
        var movie = new Movie();
        movie.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Invoice_DefaultStatus_IsPending()
    {
        var invoice = new Invoice();
        invoice.Status.Should().Be(InvoiceStatus.Pending);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    [InlineData(100)]
    public void DefaultSearchResults_TotalCount_IsSet(int count)
    {
        var results = new DefaultSearchResults<MovieDTO> { TotalCount = count };
        results.TotalCount.Should().Be(count);
    }

    [Fact]
    public void BaseEntity_CreationTime_IsUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var entity = new Movie();
        var after  = DateTime.UtcNow.AddSeconds(1);

        entity.CreationTime.Should().BeAfter(before).And.BeBefore(after);
    }
}
