using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).IsRequired().HasMaxLength(200);
        b.Property(p => p.PhotoUrl).HasMaxLength(500);
        b.HasIndex(p => p.Name).IsUnique().HasDatabaseName("IX_Person_Name");
    }
}
