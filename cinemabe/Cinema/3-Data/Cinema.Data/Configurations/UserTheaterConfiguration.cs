using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class UserTheaterConfiguration : IEntityTypeConfiguration<UserTheater>
{
    public void Configure(EntityTypeBuilder<UserTheater> b)
    {
        b.HasKey(ut => new { ut.UserId, ut.TheaterId });
        b.HasOne(ut => ut.User).WithMany(u => u.UserTheaters).HasForeignKey(ut => ut.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Theater>().WithMany().HasForeignKey(ut => ut.TheaterId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(ut => ut.TheaterId);
    }
}
