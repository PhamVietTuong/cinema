using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>
{
    public void Configure(EntityTypeBuilder<RoomType> b)
    {
        b.HasKey(rt => rt.Id);
        b.Property(rt => rt.Name).IsRequired().HasMaxLength(100);
        b.Property(rt => rt.ThreeDSurcharge).HasColumnType("float");
        b.Property(rt => rt.TurnoverBufferMinutes).HasDefaultValue(0);
        b.HasOne(rt => rt.Theater).WithMany().HasForeignKey(rt => rt.TheaterId).OnDelete(DeleteBehavior.Cascade);
    }
}
