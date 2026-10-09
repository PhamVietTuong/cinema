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

public class CashDrawerSessionConfiguration : IEntityTypeConfiguration<CashDrawerSession>
{
    public void Configure(EntityTypeBuilder<CashDrawerSession> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.TerminalName).IsRequired().HasMaxLength(100);
        b.Property(s => s.OpeningFloat).HasColumnType("float");
        b.Property(s => s.CountedCash).HasColumnType("float");
        b.Property(s => s.ExpectedCash).HasColumnType("float");
        b.Property(s => s.Variance).HasColumnType("float");
        // At most one Open session (Status = 0) per user and per terminal; enforced in SQL as well.
        b.HasIndex(s => s.UserId).IsUnique().HasFilter("[Status] = 0");
        b.HasIndex(s => new { s.TheaterId, s.TerminalName }).IsUnique().HasFilter("[Status] = 0");
        b.HasOne<Theater>().WithMany().HasForeignKey(s => s.TheaterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> b)
    {
        b.HasKey(m => m.Id);
        b.Property(m => m.Amount).HasColumnType("float");
        b.Property(m => m.Note).HasMaxLength(500);
        b.HasIndex(m => m.CashDrawerSessionId);
        b.HasOne(m => m.CashDrawerSession).WithMany(s => s.Movements).HasForeignKey(m => m.CashDrawerSessionId).OnDelete(DeleteBehavior.Restrict);
    }
}
