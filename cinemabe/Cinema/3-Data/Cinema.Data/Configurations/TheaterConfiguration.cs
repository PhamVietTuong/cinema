using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class TheaterConfiguration : IEntityTypeConfiguration<Theater>
{
    public void Configure(EntityTypeBuilder<Theater> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Name).IsRequired().HasMaxLength(200);
        b.Property(t => t.Address).IsRequired().HasMaxLength(500);
        b.Property(t => t.City).IsRequired().HasMaxLength(100);
    }
}

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

public class RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>
{
    public void Configure(EntityTypeBuilder<RoomType> b)
    {
        b.HasKey(rt => rt.Id);
        b.Property(rt => rt.Name).IsRequired().HasMaxLength(100);
        b.Property(rt => rt.ThreeDSurcharge).HasColumnType("float");
        b.HasOne(rt => rt.Theater).WithMany().HasForeignKey(rt => rt.TheaterId).OnDelete(DeleteBehavior.Cascade);
    }
}
