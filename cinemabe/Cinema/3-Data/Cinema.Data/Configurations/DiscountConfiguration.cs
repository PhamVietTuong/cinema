using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> b)
    {
        b.HasKey(d => d.Id);
        b.Property(d => d.Code).HasMaxLength(50);
        // Codes stay unique when present; auto-apply promotions may have no code (multiple NULLs allowed).
        b.HasIndex(d => d.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        b.Property(d => d.Percent).HasColumnType("float");
        b.Property(d => d.MaxDiscountAmount).HasColumnType("float");
        b.HasOne(d => d.DiscountType).WithMany(dt => dt.Discounts).HasForeignKey(d => d.DiscountTypeId).OnDelete(DeleteBehavior.Restrict);
        // Optional movie scope: null = any movie. Deleting the movie reverts the promotion to any-movie.
        b.HasOne(d => d.Movie).WithMany().HasForeignKey(d => d.MovieId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class DiscountTheaterConfiguration : IEntityTypeConfiguration<DiscountTheater>
{
    public void Configure(EntityTypeBuilder<DiscountTheater> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Discount).WithMany(d => d.DiscountTheaters).HasForeignKey(x => x.DiscountId).OnDelete(DeleteBehavior.Cascade);
        // No cascade from Theater: avoids SQL Server's multiple-cascade-path error on this join table.
        b.HasOne(x => x.Theater).WithMany().HasForeignKey(x => x.TheaterId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DiscountId, x.TheaterId }).IsUnique();
    }
}
