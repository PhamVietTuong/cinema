using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.Title).IsRequired().HasMaxLength(200);
        b.Property(i => i.Description).HasMaxLength(2000);
        b.Property(i => i.ResolutionNote).HasMaxLength(1000);
        b.HasIndex(i => new { i.TheaterId, i.Status, i.CreationTime });
    }
}
