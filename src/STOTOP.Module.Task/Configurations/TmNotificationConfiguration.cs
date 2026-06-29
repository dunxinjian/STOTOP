using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmNotificationConfiguration : IEntityTypeConfiguration<TmNotification>
{
    public void Configure(EntityTypeBuilder<TmNotification> builder)
    {
        builder.ToTable("TM站内通知");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FReceiverId).HasColumnName("F接收人ID");
        builder.Property(e => e.FEventType).HasColumnName("F事件类型");
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FContent).HasColumnName("F内容").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FRelationType).HasColumnName("F关联类型");
        builder.Property(e => e.FRelationId).HasColumnName("F关联ID");
        builder.Property(e => e.FIsRead).HasColumnName("F已读").HasDefaultValue(false);
        builder.Property(e => e.FPushedToDingTalk).HasColumnName("F已推送钉钉").HasDefaultValue(false);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FReceiverId, e.FIsRead })
            .HasDatabaseName("IX_TM站内通知_接收人_已读");
        builder.HasIndex(e => new { e.FRelationType, e.FRelationId })
            .HasDatabaseName("IX_TM站内通知_关联");
    }
}
