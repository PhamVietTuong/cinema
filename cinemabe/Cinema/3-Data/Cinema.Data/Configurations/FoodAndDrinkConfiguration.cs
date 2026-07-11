using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class FoodAndDrinkConfiguration : IEntityTypeConfiguration<FoodAndDrink>
{
    public void Configure(EntityTypeBuilder<FoodAndDrink> b)
    {
        b.HasKey(f => f.Id);
        b.Property(f => f.Name).IsRequired().HasMaxLength(200);
        b.Property(f => f.Price).HasColumnType("float");
        b.HasOne(f => f.Theater).WithMany().HasForeignKey(f => f.TheaterId).OnDelete(DeleteBehavior.Cascade);
    }
}
