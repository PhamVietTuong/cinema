using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

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
