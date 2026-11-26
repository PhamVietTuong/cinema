using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class HolidayTimeSlotPriceConfiguration : IEntityTypeConfiguration<HolidayTimeSlotPrice>
{
    public void Configure(EntityTypeBuilder<HolidayTimeSlotPrice> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.PriceMultiplier).HasColumnType("float");
        // An override is meaningless without its holiday or its time slot, so both cascade.
        b.HasOne(p => p.Holiday).WithMany().HasForeignKey(p => p.HolidayId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(p => p.TimeSlot).WithMany().HasForeignKey(p => p.TimeSlotId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(p => new { p.HolidayId, p.TimeSlotId }).IsUnique();
    }
}
