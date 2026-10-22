using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class InvoiceTicketConfiguration : IEntityTypeConfiguration<InvoiceTicket>
{
    public void Configure(EntityTypeBuilder<InvoiceTicket> b)
    {
        b.HasKey(it => new { it.InvoiceId, it.ShowTimeId, it.RoomId, it.SeatId });
        b.Property(it => it.Price).HasColumnType("float");
        b.Property(it => it.PatronCategoryName).HasMaxLength(100);
        b.Property(it => it.PatronDiscountPercent).HasColumnType("float");
        b.Property(it => it.SeatLabel).HasMaxLength(16);
        // DB-level guarantee that one seat can't be held by two active bookings across server instances.
        b.HasIndex(it => new { it.ShowTimeId, it.RoomId, it.SeatId })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
        b.HasOne(it => it.Invoice).WithMany(i => i.InvoiceTickets).HasForeignKey(it => it.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(it => it.ShowTimeRoom).WithMany(sr => sr.InvoiceTickets).HasForeignKey(it => new { it.ShowTimeId, it.RoomId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(it => it.Seat).WithMany().HasForeignKey(it => it.SeatId).OnDelete(DeleteBehavior.Restrict);
    }
}
