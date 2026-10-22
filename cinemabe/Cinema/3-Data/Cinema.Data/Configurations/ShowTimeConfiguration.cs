using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class ShowTimeConfiguration : IEntityTypeConfiguration<ShowTime>
{
    public void Configure(EntityTypeBuilder<ShowTime> b)
    {
        b.HasKey(s => s.Id);
        b.HasOne(s => s.Movie).WithMany(m => m.ShowTimes).HasForeignKey(s => s.MovieId).OnDelete(DeleteBehavior.Cascade);
    }
}
