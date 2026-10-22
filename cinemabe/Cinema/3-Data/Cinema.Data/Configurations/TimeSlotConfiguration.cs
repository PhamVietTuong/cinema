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
