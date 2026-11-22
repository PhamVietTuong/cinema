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
        // "My invoices": a user's invoices newest-first; also serves as the UserId FK index.
        b.HasIndex(i => new { i.UserId, i.CreationTime }).IsDescending(false, true).HasDatabaseName("IX_Invoice_UserId");
        // Reaper / admin list by status, newest first.
        b.HasIndex(i => new { i.Status, i.CreationTime }).HasDatabaseName("IX_Invoice_Status_CreationTime");
        // Revenue reports: Paid invoices by PaidAt, summing FinalAmount straight from the index.
        b.HasIndex(i => new { i.Status, i.PaidAt }).IncludeProperties(i => i.FinalAmount).HasDatabaseName("IX_Invoice_Status_PaidAt");
        // DiscountId is vestigial (the Discount entity/table is gone) — no FK, left as a plain column so
        // EF doesn't try to map a navigation to a type that no longer exists.
        b.Ignore(i => i.DiscountId);
        b.HasOne(i => i.PromotionProgram).WithMany().HasForeignKey(i => i.PromotionProgramId).OnDelete(DeleteBehavior.SetNull);
    }
}
