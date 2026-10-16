using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class StaffShiftConfiguration : IEntityTypeConfiguration<StaffShift>
{
    public void Configure(EntityTypeBuilder<StaffShift> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Note).HasMaxLength(500);
        b.HasIndex(s => new { s.TheaterId, s.StartTime });
        b.HasIndex(s => new { s.UserId, s.StartTime });
    }
}
