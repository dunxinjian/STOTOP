using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfDynamicStagePolicyConfiguration : IEntityTypeConfiguration<CfDynamicStagePolicy>
{
    public void Configure(EntityTypeBuilder<CfDynamicStagePolicy> builder)
    {
        builder.ToTable("CF动态审批策略");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FFlowVersionId).HasColumnName("F流程版本ID");
        builder.Property(e => e.FPolicyKey).HasColumnName("F策略键").HasMaxLength(100);
        builder.Property(e => e.FSourceStageDefinitionId).HasColumnName("F触发节点ID");
        builder.Property(e => e.FSourceStageKey).HasColumnName("F触发节点键").HasMaxLength(80);
        builder.Property(e => e.FPolicyName).HasColumnName("F策略名称").HasMaxLength(100);
        builder.Property(e => e.FStrategyType).HasColumnName("F策略类型").HasMaxLength(50);
        builder.Property(e => e.FStrategyConfigJson).HasColumnName("F策略配置JSON");
        builder.Property(e => e.FConditionJson).HasColumnName("F条件JSON");
        builder.Property(e => e.FTriggerTiming).HasColumnName("F触发时机").HasMaxLength(50).HasDefaultValue("afterSourceBeforeRoute");
        builder.Property(e => e.FInsertPosition).HasColumnName("F插入位置").HasMaxLength(50);
        builder.Property(e => e.FContinuationStageKey).HasColumnName("F续接节点键").HasMaxLength(80);
        builder.Property(e => e.FPriority).HasColumnName("F优先级");
        builder.Property(e => e.FMaxInsertCount).HasColumnName("F最大插入数量").HasDefaultValue(20);
        builder.Property(e => e.FFallbackJson).HasColumnName("F兜底JSON");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30);

        builder.HasIndex(e => new { e.FFlowVersionId, e.FPolicyKey })
            .IsUnique()
            .HasDatabaseName("IX_CF流程动态节点策略_版本策略键");
        builder.HasIndex(e => new { e.FFlowVersionId, e.FSourceStageKey, e.FPriority })
            .HasDatabaseName("IX_CF流程动态节点策略_触发优先级");
    }
}
