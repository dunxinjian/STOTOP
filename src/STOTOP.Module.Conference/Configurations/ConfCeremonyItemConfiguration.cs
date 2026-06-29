using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfCeremonyItemConfiguration : IEntityTypeConfiguration<ConfCeremonyItem>
{
    public void Configure(EntityTypeBuilder<ConfCeremonyItem> builder)
    {
        builder.ToTable("CONF仪式流程");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEventId).HasColumnName("F活动ID");
        builder.Property(e => e.FName).HasColumnName("F环节名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FStartTime).HasColumnName("F开始时间").HasMaxLength(10);
        builder.Property(e => e.FDuration).HasColumnName("F时长分钟").HasDefaultValue(5);
        builder.Property(e => e.FResponsible).HasColumnName("F负责人").HasMaxLength(50);
        builder.Property(e => e.FMusic).HasColumnName("F背景音乐").HasMaxLength(200);
        builder.Property(e => e.FLighting).HasColumnName("F灯光方案").HasMaxLength(200);
        builder.Property(e => e.FProps).HasColumnName("F道具物品");
        builder.Property(e => e.FRemark).HasColumnName("F备注");
        builder.Property(e => e.FSort).HasColumnName("F排序");
        builder.Property(e => e.FPhase).HasColumnName("F阶段").HasMaxLength(20).HasDefaultValue("仪式");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasOne(e => e.Event)
            .WithMany(ev => ev.CeremonyItems)
            .HasForeignKey(e => e.FEventId);

        builder.HasIndex(e => e.FEventId).HasDatabaseName("IX_CONF仪式流程_活动ID");
        builder.HasIndex(e => e.FSort).HasDatabaseName("IX_CONF仪式流程_排序");
    }
}
