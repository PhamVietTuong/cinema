using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class TimeSlotConfiguration : IEntityTypeConfiguration<TimeSlot>
{
    public void Configure(EntityTypeBuilder<TimeSlot> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Name).IsRequired().HasMaxLength(100);
        b.Property(t => t.StartTime).IsRequired().HasMaxLength(5);
        b.Property(t => t.EndTime).IsRequired().HasMaxLength(5);
        b.HasOne(t => t.Theater).WithMany().HasForeignKey(t => t.TheaterId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TicketPriceConfiguration : IEntityTypeConfiguration<TicketPrice>
{
    public void Configure(EntityTypeBuilder<TicketPrice> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Price).HasColumnType("float");
        b.HasOne(p => p.Theater).WithMany().HasForeignKey(p => p.TheaterId).OnDelete(DeleteBehavior.Cascade);
        // Restrict on the lookups so a referenced room type / seat type / time slot can't be silently orphaned.
        b.HasOne(p => p.RoomType).WithMany().HasForeignKey(p => p.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.SeatType).WithMany().HasForeignKey(p => p.SeatTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.TimeSlot).WithMany(t => t.TicketPrices).HasForeignKey(p => p.TimeSlotId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.TheaterId, p.RoomTypeId, p.SeatTypeId, p.TimeSlotId, p.IsHoliday }).IsUnique();
    }
}
