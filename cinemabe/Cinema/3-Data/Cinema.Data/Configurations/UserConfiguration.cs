using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(u => u.Id);
        b.Property(u => u.Name).IsRequired().HasMaxLength(200);
        b.Property(u => u.Email).IsRequired().HasMaxLength(200);
        b.Property(u => u.Phone).IsRequired().HasMaxLength(20);
        b.HasIndex(u => u.Email).IsUnique();
        b.HasIndex(u => u.Phone).IsUnique();
        b.Property(u => u.PasswordHash).IsRequired();
        b.Property(u => u.PasswordSalt).IsRequired();
        b.HasOne(u => u.UserType).WithMany(ut => ut.Users).HasForeignKey(u => u.UserTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(u => u.MemberShip).WithMany(m => m.Users).HasForeignKey(u => u.MemberShipId).OnDelete(DeleteBehavior.SetNull);
    }
}
