using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class InvoiceFoodAndDrinkConfiguration : IEntityTypeConfiguration<InvoiceFoodAndDrink>
{
    public void Configure(EntityTypeBuilder<InvoiceFoodAndDrink> b)
    {
        b.HasKey(f => new { f.InvoiceId, f.FoodAndDrinkId });
        b.Property(f => f.UnitPrice).HasColumnType("float");
        b.Property(f => f.TotalPrice).HasColumnType("float");
        b.HasOne(f => f.Invoice).WithMany(i => i.InvoiceFoodAndDrinks).HasForeignKey(f => f.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(f => f.FoodAndDrink).WithMany(fd => fd.InvoiceFoodAndDrinks).HasForeignKey(f => f.FoodAndDrinkId).OnDelete(DeleteBehavior.Restrict);
    }
}
