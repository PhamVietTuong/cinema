using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class PromotionCampaignConfiguration : IEntityTypeConfiguration<PromotionCampaign>
{
    public void Configure(EntityTypeBuilder<PromotionCampaign> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(p => p.Name).IsUnique();
    }
}
