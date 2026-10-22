using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class RoomTypePatronCategoryPriceConfiguration : IEntityTypeConfiguration<RoomTypePatronCategoryPrice>
{
    public void Configure(EntityTypeBuilder<RoomTypePatronCategoryPrice> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Price).HasColumnType("float");
        b.HasIndex(x => new { x.RoomTypeId, x.PatronCategoryId }).IsUnique();
        b.HasIndex(x => x.PatronCategoryId);
        // Both RoomType and PatronCategory already cascade from Theater, so only one leg here may
        // cascade or SQL Server rejects multiple cascade paths. PatronCategory cascades (it "owns"
        // the price being overridden); RoomType deletes are cleaned up explicitly by RoomTypeManager.
        b.HasOne(x => x.RoomType).WithMany(rt => rt.PatronCategoryPrices).HasForeignKey(x => x.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PatronCategory).WithMany(c => c.RoomTypePrices).HasForeignKey(x => x.PatronCategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}
