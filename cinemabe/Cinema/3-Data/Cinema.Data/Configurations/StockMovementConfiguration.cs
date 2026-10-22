using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.HasKey(m => m.Id);
        b.Property(m => m.Reason).HasMaxLength(500);
        b.HasOne(m => m.FoodAndDrink).WithMany().HasForeignKey(m => m.FoodAndDrinkId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(m => new { m.FoodAndDrinkId, m.CreationTime });
        b.HasIndex(m => new { m.TheaterId, m.CreationTime });
        b.HasIndex(m => m.InvoiceId);
    }
}
