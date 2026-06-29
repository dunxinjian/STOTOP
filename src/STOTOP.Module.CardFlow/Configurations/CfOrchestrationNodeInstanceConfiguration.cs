using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfOrchestrationNodeInstanceConfiguration : IEntityTypeConfiguration<CfOrchestrationNodeInstance>
{
    public void Configure(EntityTypeBuilder<CfOrchestrationNodeInstance> builder)
    {
        builder.ToTable("CF编排节点实例");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrchestrationInstanceId).HasColumnName("F编排实例ID");
        builder.Property(e => e.FNodeId).HasColumnName("F节点ID").HasMaxLength(50);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(20);
        builder.Property(e => e.FEndStatusType).HasColumnName("F终态类型").HasMaxLength(20);
        builder.Property(e => e.FRelatedCardId).HasColumnName("F关联卡片ID");
        builder.Property(e => e.FRelatedBatchId).HasColumnName("F关联批次ID");
        builder.Property(e => e.FResultJson).HasColumnName("F结果JSON");
        builder.Property(e => e.FStartTime).HasColumnName("F开始时间");
        builder.Property(e => e.FCompletedTime).HasColumnName("F完成时间");
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => new { e.FOrchestrationInstanceId, e.FNodeId })
            .IsUnique()
            .HasDatabaseName("IX_CF编排节点实例_实例节点");
        builder.HasIndex(e => e.FRelatedCardId)
            .HasFilter("[F关联卡片ID] IS NOT NULL")
            .HasDatabaseName("IX_CF编排节点实例_卡片");
        builder.HasIndex(e => e.FRelatedBatchId)
            .HasFilter("[F关联批次ID] IS NOT NULL")
            .HasDatabaseName("IX_CF编排节点实例_批次");
    }
}
