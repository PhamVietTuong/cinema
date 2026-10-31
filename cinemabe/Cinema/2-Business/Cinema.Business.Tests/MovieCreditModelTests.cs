using Cinema.Data.Contexts;
using Cinema.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cinema.Business.Tests;

/// <summary>Asserts the MovieCredit EF model shape against metadata only — no database connection is
/// opened, so this runs anywhere `dotnet test` runs.</summary>
public class MovieCreditModelTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<CinemaContext>()
            .UseSqlServer("Server=.;Database=ModelMetadataOnly;TrustServerCertificate=True")
            .Options;
        using var context = new CinemaContext(options);
        return context.Model;
    }

    [Fact]
    public void MovieCredit_MovieForeignKey_CascadesOnDelete()
    {
        var entity = BuildModel().FindEntityType(typeof(MovieCredit))!;
        var fk = entity.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(Movie));

        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void MovieCredit_PersonForeignKey_RestrictsOnDelete()
    {
        var entity = BuildModel().FindEntityType(typeof(MovieCredit))!;
        var fk = entity.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(Person));

        fk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void MovieCredit_HasUniqueIndexOnMoviePersonCreditType()
    {
        var entity = BuildModel().FindEntityType(typeof(MovieCredit))!;

        var index = entity.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(MovieCredit.MovieId), nameof(MovieCredit.PersonId), nameof(MovieCredit.CreditType) }));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void MovieCredit_CharacterName_HasMaxLength200()
    {
        var entity = BuildModel().FindEntityType(typeof(MovieCredit))!;
        var property = entity.FindProperty(nameof(MovieCredit.CharacterName))!;

        property.GetMaxLength().Should().Be(200);
    }
}
