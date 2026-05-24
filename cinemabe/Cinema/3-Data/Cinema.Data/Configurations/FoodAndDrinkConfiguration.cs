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
    }
}

public class FoodAndDrinkTheaterConfiguration : IEntityTypeConfiguration<FoodAndDrinkTheater>
{
    public void Configure(EntityTypeBuilder<FoodAndDrinkTheater> b)
    {
        b.HasKey(ft => new { ft.FoodAndDrinkId, ft.TheaterId });
        b.HasOne(ft => ft.FoodAndDrink).WithMany(f => f.FoodAndDrinkTheaters).HasForeignKey(ft => ft.FoodAndDrinkId);
        b.HasOne(ft => ft.Theater).WithMany(t => t.FoodAndDrinkTheaters).HasForeignKey(ft => ft.TheaterId);
    }
}
