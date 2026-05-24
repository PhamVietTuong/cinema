using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> b)
    {
        b.HasKey(m => m.Id);
        b.Property(m => m.Title).IsRequired().HasMaxLength(300);
        b.Property(m => m.Description).HasMaxLength(2000);
        b.Property(m => m.Director).HasMaxLength(200);
        b.Property(m => m.Cast).HasMaxLength(1000);
        b.Property(m => m.Language).HasMaxLength(100);
        b.HasOne(m => m.AgeRestriction).WithMany(a => a.Movies).HasForeignKey(m => m.AgeRestrictionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MovieTypeDetailConfiguration : IEntityTypeConfiguration<MovieTypeDetail>
{
    public void Configure(EntityTypeBuilder<MovieTypeDetail> b)
    {
        b.HasKey(mt => new { mt.MovieId, mt.MovieTypeId });
        b.HasOne(mt => mt.Movie).WithMany(m => m.MovieTypeDetails).HasForeignKey(mt => mt.MovieId);
        b.HasOne(mt => mt.MovieType).WithMany(t => t.MovieTypeDetails).HasForeignKey(mt => mt.MovieTypeId);
    }
}
