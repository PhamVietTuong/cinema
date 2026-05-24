using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> b)
    {
        b.HasKey(d => d.Id);
        b.Property(d => d.Code).IsRequired().HasMaxLength(50);
        b.HasIndex(d => d.Code).IsUnique();
        b.Property(d => d.Percent).HasColumnType("float");
        b.Property(d => d.MaxDiscountAmount).HasColumnType("float");
        b.HasOne(d => d.DiscountType).WithMany(dt => dt.Discounts).HasForeignKey(d => d.DiscountTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
