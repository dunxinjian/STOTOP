using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfTodoItemConfiguration : IEntityTypeConfiguration<CfTodoItem>
{
    public void Configure(EntityTypeBuilder<CfTodoItem> builder)
    {
        builder.ToTable("CF待办项");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCardId).HasColumnName("F卡片ID");
        builder.Property(e => e.FStageInstanceId).HasColumnName("F节点实例ID");
        builder.Property(e => e.FHandlerId).HasColumnName("F处理人ID");
        builder.Property(e => e.FHandlerName).HasColumnName("F处理人姓名").HasMaxLength(100);
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200);
        builder.Property(e => e.FType).HasColumnName("F类型").HasMaxLength(30);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30);
        builder.Property(e => e.FPriority).HasColumnName("F优先级");
        builder.Property(e => e.FDelegateSourceId).HasColumnName("F委托来源ID");
        builder.Property(e => e.FPushChannel).HasColumnName("F外部推送渠道").HasMaxLength(30);
        builder.Property(e => e.FExternalTodoId).HasColumnName("F外部待办ID").HasMaxLength(200);
        builder.Property(e => e.FPushStatus).HasColumnName("F推送状态").HasMaxLength(30);
        builder.Property(e => e.FRetryCount).HasColumnName("F重试次数");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FCompletedTime).HasColumnName("F完成时间");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");

        builder.HasIndex(e => new { e.FHandlerId, e.FStatus }).HasDatabaseName("IX_CF待办项_处理人");
        builder.HasIndex(e => e.FCardId).HasDatabaseName("IX_CF待办项_卡片");
        builder.HasIndex(e => new { e.FOrgId, e.FStatus, e.FPriority }).HasDatabaseName("IX_CF待办项_组织状态");
    }
}
