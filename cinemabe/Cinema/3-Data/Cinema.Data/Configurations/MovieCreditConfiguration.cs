using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class MovieCreditConfiguration : IEntityTypeConfiguration<MovieCredit>
{
    public void Configure(EntityTypeBuilder<MovieCredit> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.CharacterName).HasMaxLength(200);
        b.HasOne(c => c.Movie).WithMany(m => m.MovieCredits).HasForeignKey(c => c.MovieId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(c => c.Person).WithMany().HasForeignKey(c => c.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => new { c.MovieId, c.PersonId, c.CreditType }).IsUnique().HasDatabaseName("IX_MovieCredit_MovieId_PersonId_CreditType");
        b.HasIndex(c => c.PersonId).HasDatabaseName("IX_MovieCredit_PersonId");
    }
}
