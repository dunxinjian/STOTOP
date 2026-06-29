using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfDispatchRecordConfiguration : IEntityTypeConfiguration<CfDispatchRecord>
{
    public void Configure(EntityTypeBuilder<CfDispatchRecord> builder)
    {
        builder.ToTable("CF派发记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrchestrationInstanceId).HasColumnName("F编排实例ID");
        builder.Property(e => e.FDispatchType).HasColumnName("F派发类型").HasMaxLength(20);
        builder.Property(e => e.FSourceNodeId).HasColumnName("F源节点ID").HasMaxLength(50);
        builder.Property(e => e.FSourceCardId).HasColumnName("F源卡片ID");
        builder.Property(e => e.FSourceFlowCode).HasColumnName("F源流程编码").HasMaxLength(50);
        builder.Property(e => e.FTargetNodeId).HasColumnName("F目标节点ID").HasMaxLength(50);
        builder.Property(e => e.FTargetCardId).HasColumnName("F目标卡片ID");
        builder.Property(e => e.FTargetFlowCode).HasColumnName("F目标流程编码").HasMaxLength(50);
        builder.Property(e => e.FDataPayloadJson).HasColumnName("F数据载荷JSON");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(20);
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FTriggeredTime).HasColumnName("F触发时间");
        builder.Property(e => e.FFailureReason).HasColumnName("F失败原因").HasMaxLength(500);
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => e.FOrchestrationInstanceId)
            .HasFilter("[F编排实例ID] IS NOT NULL")
            .HasDatabaseName("IX_CF派发记录_编排实例");
        builder.HasIndex(e => e.FSourceCardId)
            .HasFilter("[F源卡片ID] IS NOT NULL")
            .HasDatabaseName("IX_CF派发记录_源卡片");
        builder.HasIndex(e => new { e.FDispatchType, e.FStatus })
            .HasDatabaseName("IX_CF派发记录_类型状态");
    }
}
