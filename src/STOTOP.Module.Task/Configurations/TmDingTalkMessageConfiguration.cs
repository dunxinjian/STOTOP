using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmDingTalkMessageConfiguration : IEntityTypeConfiguration<TmDingTalkMessage>
{
    public void Configure(EntityTypeBuilder<TmDingTalkMessage> builder)
    {
        builder.ToTable("TM钉钉消息推送");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FSourceType).HasColumnName("F来源类型");
        builder.Property(e => e.FSourceId).HasColumnName("F来源ID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID");
        builder.Property(e => e.FSenderId).HasColumnName("F发送人ID");
        builder.Property(e => e.FPushStatus).HasColumnName("F推送状态").HasDefaultValue(0);
        builder.Property(e => e.FDingTalkMessageId).HasColumnName("F钉钉消息ID").HasMaxLength(100);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FPushStatus).HasDatabaseName("IX_TM钉钉消息推送_推送状态");

        builder.HasOne(e => e.Task)
            .WithMany()
            .HasForeignKey(e => e.FTaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
