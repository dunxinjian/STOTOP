using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfOrchestrationInstanceConfiguration : IEntityTypeConfiguration<CfOrchestrationInstance>
{
    public void Configure(EntityTypeBuilder<CfOrchestrationInstance> builder)
    {
        builder.ToTable("CF编排实例");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTemplateId).HasColumnName("F编排模板ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(20);
        builder.Property(e => e.FCompletionReason).HasColumnName("F完成原因").HasMaxLength(20);
        builder.Property(e => e.FSnapshotNodesJson).HasColumnName("F快照节点JSON");
        builder.Property(e => e.FSnapshotEdgesJson).HasColumnName("F快照边JSON");
        builder.Property(e => e.FContextJson).HasColumnName("F上下文JSON");
        builder.Property(e => e.FTriggerCount).HasColumnName("F已触发次数");
        builder.Property(e => e.FInitiatorId).HasColumnName("F发起人ID");
        builder.Property(e => e.FInitiatedTime).HasColumnName("F发起时间");
        builder.Property(e => e.FCompletedTime).HasColumnName("F完成时间");
        builder.Property(e => e.FFailureReason).HasColumnName("F失败原因").HasMaxLength(500);
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => e.FTemplateId).HasDatabaseName("IX_CF编排实例_模板");
        builder.HasIndex(e => new { e.FOrgId, e.FStatus }).HasDatabaseName("IX_CF编排实例_组织状态");
        builder.HasIndex(e => e.FInitiatorId).HasDatabaseName("IX_CF编排实例_发起人");
    }
}
