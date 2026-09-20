using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.RowName).IsRequired().HasMaxLength(5);
        b.HasIndex(s => new { s.RoomId, s.RowName, s.ColIndex }).IsUnique();
        b.HasIndex(s => s.SeatGroupId);
        b.HasOne(s => s.Room).WithMany(r => r.Seats).HasForeignKey(s => s.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SeatTypeConfiguration : IEntityTypeConfiguration<SeatType>
{
    public void Configure(EntityTypeBuilder<SeatType> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(s => new { s.TheaterId, s.Kind }).IsUnique();
        b.HasOne(s => s.Theater).WithMany().HasForeignKey(s => s.TheaterId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PatronCategoryConfiguration : IEntityTypeConfiguration<PatronCategory>
{
    public void Configure(EntityTypeBuilder<PatronCategory> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Name).IsRequired().HasMaxLength(100);
        b.Property(c => c.Price).HasColumnType("float");
        b.HasIndex(c => new { c.TheaterId, c.Name, c.SeatTypeId }).IsUnique();
        b.HasOne(c => c.Theater).WithMany().HasForeignKey(c => c.TheaterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(c => c.SeatType).WithMany(s => s.PatronCategories).HasForeignKey(c => c.SeatTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

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
