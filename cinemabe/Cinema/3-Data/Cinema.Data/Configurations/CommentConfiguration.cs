using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Content).IsRequired().HasMaxLength(2000);
        b.HasOne(c => c.Movie).WithMany(m => m.Comments).HasForeignKey(c => c.MovieId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(c => c.User).WithMany(u => u.Comments).HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(c => c.Parent).WithMany(c => c.Replies).HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

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
