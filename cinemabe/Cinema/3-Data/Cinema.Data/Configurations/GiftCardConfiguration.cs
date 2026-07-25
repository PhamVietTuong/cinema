using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class GiftCardConfiguration : IEntityTypeConfiguration<GiftCard>
{
    public void Configure(EntityTypeBuilder<GiftCard> b)
    {
        b.HasKey(g => g.Id);
        b.Property(g => g.Code).IsRequired().HasMaxLength(50);
        b.HasIndex(g => g.Code).IsUnique();
        b.Property(g => g.InitialBalance).HasColumnType("float");
        b.Property(g => g.Balance).HasColumnType("float");
    }
}
