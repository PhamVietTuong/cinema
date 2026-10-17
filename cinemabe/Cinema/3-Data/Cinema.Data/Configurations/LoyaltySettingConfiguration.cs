using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class LoyaltySettingConfiguration : IEntityTypeConfiguration<LoyaltySetting>
{
    public void Configure(EntityTypeBuilder<LoyaltySetting> b)
    {
        b.HasKey(s => s.Id);
        b.HasIndex(s => s.TheaterId).IsUnique().HasFilter("[TheaterId] IS NOT NULL");
    }
}
