using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

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
