using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class InvoicePaymentConfiguration : IEntityTypeConfiguration<InvoicePayment>
{
    public void Configure(EntityTypeBuilder<InvoicePayment> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Amount).HasColumnType("float");
        b.Property(p => p.TenderedAmount).HasColumnType("float");
        b.Property(p => p.ChangeAmount).HasColumnType("float");
        b.Property(p => p.Reference).HasMaxLength(100);
        b.HasIndex(p => p.InvoiceId);
        b.HasOne(p => p.Invoice).WithMany(i => i.Payments).HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}
