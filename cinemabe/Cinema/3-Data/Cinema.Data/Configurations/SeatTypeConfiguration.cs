using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class SeatTypeConfiguration : IEntityTypeConfiguration<SeatType>
{
    public void Configure(EntityTypeBuilder<SeatType> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(s => new { s.TheaterId, s.Kind }).IsUnique();
        b.HasOne(s => s.Theater).WithMany().HasForeignKey(s => s.TheaterId).OnDelete(DeleteBehavior.Cascade);
    }
}
