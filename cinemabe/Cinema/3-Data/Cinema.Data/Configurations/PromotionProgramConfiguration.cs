using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class PromotionProgramConfiguration : IEntityTypeConfiguration<PromotionProgram>
{
    public void Configure(EntityTypeBuilder<PromotionProgram> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Title).IsRequired().HasMaxLength(200);
        b.Property(p => p.Summary).HasMaxLength(500);
        b.Property(p => p.PercentOff).HasColumnType("float");
        b.Property(p => p.TermsText).HasMaxLength(2000);
        b.HasIndex(p => new { p.IsPublished, p.EndDate });
        b.HasIndex(p => p.MinMemberShipId);
        b.HasIndex(p => p.LinkedDiscountId);
        // A program references a tier it requires — deleting that tier should not silently open the
        // program to everyone, so it is blocked (NO ACTION / Restrict), same stance as DiscountTheater.
        b.HasOne(p => p.MinMemberShip).WithMany().HasForeignKey(p => p.MinMemberShipId).OnDelete(DeleteBehavior.Restrict);
        // Deleting the linked Discount just unlinks the program (it stops showing a code).
        b.HasOne(p => p.LinkedDiscount).WithMany().HasForeignKey(p => p.LinkedDiscountId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PromotionProgramPriceConfiguration : IEntityTypeConfiguration<PromotionProgramPrice>
{
    public void Configure(EntityTypeBuilder<PromotionProgramPrice> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Price).HasColumnType("float");
        b.HasIndex(x => new { x.PromotionProgramId, x.ProjectionForm, x.SeatKind }).IsUnique();
        b.HasOne(x => x.PromotionProgram).WithMany(p => p.Prices).HasForeignKey(x => x.PromotionProgramId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PromotionProgramScheduleConfiguration : IEntityTypeConfiguration<PromotionProgramSchedule>
{
    public void Configure(EntityTypeBuilder<PromotionProgramSchedule> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.PromotionProgramId);
        b.HasIndex(x => x.TheaterId);
        b.HasOne(x => x.PromotionProgram).WithMany(p => p.Schedules).HasForeignKey(x => x.PromotionProgramId).OnDelete(DeleteBehavior.Cascade);
        // No cascade from Theater: avoids SQL Server's multiple-cascade-path error, same as DiscountTheater.
        b.HasOne(x => x.Theater).WithMany().HasForeignKey(x => x.TheaterId).OnDelete(DeleteBehavior.Restrict);
    }
}
