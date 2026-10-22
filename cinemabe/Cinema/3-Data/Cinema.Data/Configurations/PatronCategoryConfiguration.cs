using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class PatronCategoryConfiguration : IEntityTypeConfiguration<PatronCategory>
{
    public void Configure(EntityTypeBuilder<PatronCategory> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Name).IsRequired().HasMaxLength(100);
        b.Property(c => c.Price).HasColumnType("float");
        b.Property(c => c.RequiresIdCheck).HasDefaultValue(false);
        b.Property(c => c.IdCheckMessage).HasMaxLength(1000);
        b.HasIndex(c => new { c.TheaterId, c.Name, c.SeatTypeId }).IsUnique();
        b.HasOne(c => c.Theater).WithMany().HasForeignKey(c => c.TheaterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(c => c.SeatType).WithMany(s => s.PatronCategories).HasForeignKey(c => c.SeatTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
