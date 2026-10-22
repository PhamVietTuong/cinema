using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class PromotionProgramScheduleConfiguration : IEntityTypeConfiguration<PromotionProgramSchedule>
{
    public void Configure(EntityTypeBuilder<PromotionProgramSchedule> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.PromotionProgramId);
        b.HasIndex(x => x.TheaterId);
        b.HasOne(x => x.PromotionProgram).WithMany(p => p.Schedules).HasForeignKey(x => x.PromotionProgramId).OnDelete(DeleteBehavior.Cascade);
        // No cascade from Theater: avoids SQL Server's multiple-cascade-path error, same as DiscountTheater.
        b.HasOne(x => x.Theater).WithMany().HasForeignKey(x => x.TheaterId).OnDelete(DeleteBehavior.Restrict);
    }
}
