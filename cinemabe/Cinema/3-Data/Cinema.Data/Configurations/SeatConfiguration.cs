using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.RowName).IsRequired().HasMaxLength(5);
        b.HasIndex(s => new { s.RoomId, s.RowName, s.ColIndex }).IsUnique();
        b.HasOne(s => s.Room).WithMany(r => r.Seats).HasForeignKey(s => s.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.SeatType).WithMany(st => st.Seats).HasForeignKey(s => s.SeatTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SeatTypeTicketTypeConfiguration : IEntityTypeConfiguration<SeatTypeTicketType>
{
    public void Configure(EntityTypeBuilder<SeatTypeTicketType> b)
    {
        b.HasKey(s => new { s.SeatTypeId, s.TicketTypeId });
        b.Property(s => s.PriceMultiplier).HasColumnType("float");
        b.HasOne(s => s.SeatType).WithMany(st => st.SeatTypeTicketTypes).HasForeignKey(s => s.SeatTypeId);
        b.HasOne(s => s.TicketType).WithMany(tt => tt.SeatTypeTicketTypes).HasForeignKey(s => s.TicketTypeId);
    }
}
