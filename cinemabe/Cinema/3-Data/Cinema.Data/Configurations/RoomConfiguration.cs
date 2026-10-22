using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.Name).IsRequired().HasMaxLength(100);
        b.HasOne(r => r.Theater).WithMany(t => t.Rooms).HasForeignKey(r => r.TheaterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.RoomType).WithMany(rt => rt.Rooms).HasForeignKey(r => r.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
