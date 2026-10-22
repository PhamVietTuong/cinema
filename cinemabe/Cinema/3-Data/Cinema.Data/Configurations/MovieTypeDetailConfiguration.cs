using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class MovieTypeDetailConfiguration : IEntityTypeConfiguration<MovieTypeDetail>
{
    public void Configure(EntityTypeBuilder<MovieTypeDetail> b)
    {
        b.HasKey(mt => new { mt.MovieId, mt.MovieTypeId });
        b.HasOne(mt => mt.Movie).WithMany(m => m.MovieTypeDetails).HasForeignKey(mt => mt.MovieId);
        b.HasOne(mt => mt.MovieType).WithMany(t => t.MovieTypeDetails).HasForeignKey(mt => mt.MovieTypeId);
    }
}
