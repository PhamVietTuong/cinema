using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class PromotionProgramConfiguration : IEntityTypeConfiguration<PromotionProgram>
{
    public void Configure(EntityTypeBuilder<PromotionProgram> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Title).IsRequired().HasMaxLength(200);
        b.Property(p => p.Summary).HasMaxLength(500);
        b.Property(p => p.PercentOff).HasColumnType("float");
        b.Property(p => p.MaxDiscountAmount).HasColumnType("float");
        b.Property(p => p.TermsText).HasMaxLength(2000);
        b.Property(p => p.Code).HasMaxLength(50);
        // Codes stay unique when present; auto-apply/info programs may have no code (multiple NULLs allowed).
        b.HasIndex(p => p.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        b.HasIndex(p => new { p.IsPublished, p.EndDate });
        b.HasIndex(p => p.MinMemberShipId);
        // A program references a tier it requires — deleting that tier should not silently open the
        // program to everyone, so it is blocked (NO ACTION / Restrict), same stance as the movie scope below.
        b.HasOne(p => p.MinMemberShip).WithMany().HasForeignKey(p => p.MinMemberShipId).OnDelete(DeleteBehavior.Restrict);
        // Optional movie scope: null = any movie. Deleting the movie reverts the program to any-movie.
        b.HasOne(p => p.Movie).WithMany().HasForeignKey(p => p.MovieId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(p => p.CampaignId);
        // Optional campaign grouping: null = no campaign (still shown under "All" on the customer page).
        // Deleting a campaign reverts its programs to uncategorized rather than blocking the delete.
        b.HasOne(p => p.Campaign).WithMany().HasForeignKey(p => p.CampaignId).OnDelete(DeleteBehavior.SetNull);
    }
}
