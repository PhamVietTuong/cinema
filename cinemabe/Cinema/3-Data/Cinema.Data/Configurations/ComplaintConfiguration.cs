using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Description).IsRequired().HasMaxLength(2000);
        b.Property(c => c.CompensationRef).HasMaxLength(100);
        b.Property(c => c.ResolutionNote).HasMaxLength(1000);
        b.HasIndex(c => new { c.TheaterId, c.Status, c.CreationTime });
    }
}
