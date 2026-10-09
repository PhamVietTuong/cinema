using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Data.Configurations;

public class ChecklistTemplateConfiguration : IEntityTypeConfiguration<ChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplate> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Name).IsRequired().HasMaxLength(200);
        b.HasIndex(t => new { t.TheaterId, t.Kind }).IsUnique().HasFilter("[IsActive] = 1");
    }
}

public class ChecklistTemplateItemConfiguration : IEntityTypeConfiguration<ChecklistTemplateItem>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplateItem> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.Text).IsRequired().HasMaxLength(300);
        b.HasOne(i => i.ChecklistTemplate).WithMany(t => t.Items).HasForeignKey(i => i.ChecklistTemplateId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(i => i.ChecklistTemplateId);
    }
}

public class ChecklistRunConfiguration : IEntityTypeConfiguration<ChecklistRun>
{
    public void Configure(EntityTypeBuilder<ChecklistRun> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.TemplateName).IsRequired().HasMaxLength(200);
        b.HasIndex(r => new { r.ShowTimeId, r.RoomId, r.Kind }).IsUnique();
        b.HasIndex(r => new { r.TheaterId, r.CreationTime });
    }
}

public class ChecklistRunItemConfiguration : IEntityTypeConfiguration<ChecklistRunItem>
{
    public void Configure(EntityTypeBuilder<ChecklistRunItem> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.Text).IsRequired().HasMaxLength(300);
        b.Property(i => i.Note).HasMaxLength(500);
        b.HasOne(i => i.ChecklistRun).WithMany(r => r.Items).HasForeignKey(i => i.ChecklistRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(i => i.ChecklistRunId);
    }
}
