using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class ComboItemConfiguration : IEntityTypeConfiguration<ComboItem>
{
    public void Configure(EntityTypeBuilder<ComboItem> b)
    {
        b.HasKey(c => c.Id);
        b.HasIndex(c => new { c.ComboId, c.ComponentId }).IsUnique();
        b.HasOne(c => c.Combo).WithMany(f => f.ComboItems).HasForeignKey(c => c.ComboId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(c => c.Component).WithMany().HasForeignKey(c => c.ComponentId).OnDelete(DeleteBehavior.NoAction);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ComboItem_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_ComboItem_NotSelf", "[ComboId] <> [ComponentId]");
        });
    }
}

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
