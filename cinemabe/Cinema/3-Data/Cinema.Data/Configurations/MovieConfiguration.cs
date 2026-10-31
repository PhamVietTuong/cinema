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
        b.Property(m => m.Slug).IsRequired().HasMaxLength(320);
        b.HasIndex(m => m.Slug).IsUnique().HasDatabaseName("IX_Movie_Slug");
        b.Property(m => m.Description).HasMaxLength(2000);
        b.Property(m => m.Director).HasMaxLength(200);
        b.Property(m => m.Cast).HasMaxLength(1000);
        b.Property(m => m.Language).HasMaxLength(100);
        b.Property(m => m.ImdbId).HasMaxLength(20);
        b.HasOne(m => m.AgeRestriction).WithMany(a => a.Movies).HasForeignKey(m => m.AgeRestrictionId).OnDelete(DeleteBehavior.Restrict);
    }
}
