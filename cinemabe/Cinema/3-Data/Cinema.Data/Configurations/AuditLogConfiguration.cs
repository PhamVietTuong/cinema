using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        b.Property(a => a.Reason).HasMaxLength(500);
        b.Property(a => a.Amount).HasColumnType("float");
        b.HasIndex(a => new { a.TheaterId, a.CreationTime });
        b.HasIndex(a => new { a.ActorUserId, a.CreationTime });
    }
}
