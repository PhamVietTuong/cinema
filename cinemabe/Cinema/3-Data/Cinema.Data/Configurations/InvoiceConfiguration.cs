using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.Code).IsRequired().HasMaxLength(50);
        b.HasIndex(i => i.Code).IsUnique();
        b.Property(i => i.TotalAmount).HasColumnType("float");
        b.Property(i => i.DiscountAmount).HasColumnType("float");
        b.Property(i => i.FinalAmount).HasColumnType("float");
        // UserId is null for a walk-in counter sale.
        b.HasOne(i => i.User).WithMany(u => u.Invoices).HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Theater>().WithMany().HasForeignKey(i => i.TheaterId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(i => new { i.TheaterId, i.PaidAt });
        b.HasOne(i => i.Discount).WithMany().HasForeignKey(i => i.DiscountId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(i => i.PromotionProgram).WithMany().HasForeignKey(i => i.PromotionProgramId).OnDelete(DeleteBehavior.SetNull);
    }
}
