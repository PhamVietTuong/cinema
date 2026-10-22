using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class TicketPriceConfiguration : IEntityTypeConfiguration<TicketPrice>
{
    public void Configure(EntityTypeBuilder<TicketPrice> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.PriceMultiplier).HasColumnType("float");
        b.HasOne(p => p.Theater).WithMany().HasForeignKey(p => p.TheaterId).OnDelete(DeleteBehavior.Cascade);
        // Restrict on the lookups so a referenced room type / seat type / time slot can't be silently orphaned.
        b.HasOne(p => p.RoomType).WithMany().HasForeignKey(p => p.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.TimeSlot).WithMany(t => t.TicketPrices).HasForeignKey(p => p.TimeSlotId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.TheaterId, p.RoomTypeId, p.TimeSlotId, p.DayOfWeek }).IsUnique();
    }
}
