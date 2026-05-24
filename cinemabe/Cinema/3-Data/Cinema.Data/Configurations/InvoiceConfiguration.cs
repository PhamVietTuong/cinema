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
        b.HasOne(i => i.User).WithMany(u => u.Invoices).HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Discount).WithMany().HasForeignKey(i => i.DiscountId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class InvoiceTicketConfiguration : IEntityTypeConfiguration<InvoiceTicket>
{
    public void Configure(EntityTypeBuilder<InvoiceTicket> b)
    {
        b.HasKey(it => new { it.InvoiceId, it.ShowTimeId, it.RoomId, it.SeatId });
        b.Property(it => it.Price).HasColumnType("float");
        b.HasOne(it => it.Invoice).WithMany(i => i.InvoiceTickets).HasForeignKey(it => it.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(it => it.ShowTimeRoom).WithMany(sr => sr.InvoiceTickets).HasForeignKey(it => new { it.ShowTimeId, it.RoomId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(it => it.Seat).WithMany().HasForeignKey(it => it.SeatId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(it => it.TicketType).WithMany(tt => tt.InvoiceTickets).HasForeignKey(it => it.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InvoiceFoodAndDrinkConfiguration : IEntityTypeConfiguration<InvoiceFoodAndDrink>
{
    public void Configure(EntityTypeBuilder<InvoiceFoodAndDrink> b)
    {
        b.HasKey(f => new { f.InvoiceId, f.FoodAndDrinkId });
        b.Property(f => f.UnitPrice).HasColumnType("float");
        b.Property(f => f.TotalPrice).HasColumnType("float");
        b.HasOne(f => f.Invoice).WithMany(i => i.InvoiceFoodAndDrinks).HasForeignKey(f => f.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(f => f.FoodAndDrink).WithMany(fd => fd.InvoiceFoodAndDrinks).HasForeignKey(f => f.FoodAndDrinkId).OnDelete(DeleteBehavior.Restrict);
    }
}
