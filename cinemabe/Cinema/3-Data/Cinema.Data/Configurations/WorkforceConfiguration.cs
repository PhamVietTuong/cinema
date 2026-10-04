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

public class TimeClockEntryConfiguration : IEntityTypeConfiguration<TimeClockEntry>
{
    public void Configure(EntityTypeBuilder<TimeClockEntry> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Note).HasMaxLength(500);
        // One open (not yet clocked out) entry per user, enforced by the database.
        b.HasIndex(e => e.UserId).IsUnique().HasFilter("[ClockOutAt] IS NULL").HasDatabaseName("IX_TimeClockEntry_UserId_Open");
        b.HasIndex(e => new { e.TheaterId, e.ClockInAt });
    }
}

public class StaffTaskConfiguration : IEntityTypeConfiguration<StaffTask>
{
    public void Configure(EntityTypeBuilder<StaffTask> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Title).IsRequired().HasMaxLength(200);
        b.Property(t => t.Description).HasMaxLength(2000);
        b.HasIndex(t => new { t.AssignedToUserId, t.Status });
        b.HasIndex(t => new { t.TheaterId, t.Status, t.CreationTime });
    }
}
