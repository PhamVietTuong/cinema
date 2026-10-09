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

public class StoragePlanConfiguration : IEntityTypeConfiguration<StoragePlan>
{
    public void Configure(EntityTypeBuilder<StoragePlan> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Code).IsRequired().HasMaxLength(30);
        b.HasIndex(p => p.Code).IsUnique();
        b.Property(p => p.Supplier).HasMaxLength(200);
        b.Property(p => p.Note).HasMaxLength(1000);
        b.Property(p => p.RejectionReason).HasMaxLength(500);
        b.Property(p => p.RowVersion).IsRowVersion();
        b.HasOne(p => p.Theater).WithMany().HasForeignKey(p => p.TheaterId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(p => new { p.TheaterId, p.Status });
    }
}

public class StoragePlanItemConfiguration : IEntityTypeConfiguration<StoragePlanItem>
{
    public void Configure(EntityTypeBuilder<StoragePlanItem> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.UnitCost).HasColumnType("float");
        b.Property(i => i.Note).HasMaxLength(500);
        b.HasOne(i => i.StoragePlan).WithMany(p => p.Items).HasForeignKey(i => i.StoragePlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(i => i.FoodAndDrink).WithMany().HasForeignKey(i => i.FoodAndDrinkId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(i => new { i.StoragePlanId, i.FoodAndDrinkId }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_StoragePlanItem_PlannedQuantity", "[PlannedQuantity] > 0"));
    }
}
