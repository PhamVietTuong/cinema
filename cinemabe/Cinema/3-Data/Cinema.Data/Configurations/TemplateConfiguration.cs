// Trivial entity configurations (AgeRestriction, Holiday, News) are handled by EF Core
// conventions (Id primary key by default) and need no explicit configuration here.
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Code).HasMaxLength(100).IsRequired();
        b.HasIndex(t => t.Code).IsUnique();
        b.Property(t => t.Name).HasMaxLength(200).IsRequired();
        b.Property(t => t.Subject).HasMaxLength(500);
        b.Property(t => t.Body).IsRequired();
    }
}
