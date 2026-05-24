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

public class ShowTimeRoomConfiguration : IEntityTypeConfiguration<ShowTimeRoom>
{
    public void Configure(EntityTypeBuilder<ShowTimeRoom> b)
    {
        b.HasKey(sr => new { sr.ShowTimeId, sr.RoomId });
        b.Property(sr => sr.BasePrice).HasColumnType("float");
        b.HasOne(sr => sr.ShowTime).WithMany(s => s.ShowTimeRooms).HasForeignKey(sr => sr.ShowTimeId);
        b.HasOne(sr => sr.Room).WithMany(r => r.ShowTimeRooms).HasForeignKey(sr => sr.RoomId);
    }
}
