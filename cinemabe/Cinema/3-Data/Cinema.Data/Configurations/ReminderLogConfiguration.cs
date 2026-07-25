using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class ReminderLogConfiguration : IEntityTypeConfiguration<ReminderLog>
{
    public void Configure(EntityTypeBuilder<ReminderLog> b)
    {
        b.HasKey(r => r.Id);
        // One reminder per (user, showtime) — also makes WasSentAsync lookups fast.
        b.HasIndex(r => new { r.UserId, r.ShowTimeId }).IsUnique();
    }
}
