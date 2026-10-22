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
