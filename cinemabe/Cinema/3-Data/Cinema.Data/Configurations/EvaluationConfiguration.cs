using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    public void Configure(EntityTypeBuilder<Evaluation> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.MovieId, e.UserId }).IsUnique();
        b.Property(e => e.Score).IsRequired();
        b.Property(e => e.Review).HasMaxLength(1000);
        b.HasOne(e => e.Movie).WithMany(m => m.Evaluations).HasForeignKey(e => e.MovieId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(e => e.User).WithMany(u => u.Evaluations).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
