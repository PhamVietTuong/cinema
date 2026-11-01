using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class SplashBannerConfiguration : IEntityTypeConfiguration<SplashBanner>
{
    public void Configure(EntityTypeBuilder<SplashBanner> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Title).IsRequired().HasMaxLength(200);
        b.Property(p => p.ImageUrl).IsRequired().HasMaxLength(500);
        b.Property(p => p.LinkUrl).HasMaxLength(500);
        b.HasIndex(p => new { p.IsPublished, p.EndDate });
    }
}
