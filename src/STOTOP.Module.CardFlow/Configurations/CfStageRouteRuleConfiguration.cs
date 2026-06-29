using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfStageRouteRuleConfiguration : IEntityTypeConfiguration<CfStageRouteRule>
{
    public void Configure(EntityTypeBuilder<CfStageRouteRule> builder)
    {
        builder.ToTable("CF节点流转规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FFlowVersionId).HasColumnName("F流程版本ID");
        builder.Property(e => e.FEdgeKey).HasColumnName("F边键").HasMaxLength(100);
        builder.Property(e => e.FFromStageDefinitionId).HasColumnName("F来源节点ID");
        builder.Property(e => e.FFromStageKey).HasColumnName("F来源节点键").HasMaxLength(80);
        builder.Property(e => e.FToStageDefinitionId).HasColumnName("F目标节点ID");
        builder.Property(e => e.FToStageKey).HasColumnName("F目标节点键").HasMaxLength(80);
        builder.Property(e => e.FRouteName).HasColumnName("F规则名称").HasMaxLength(100);
        builder.Property(e => e.FConditionJson).HasColumnName("F条件JSON");
        builder.Property(e => e.FPriority).HasColumnName("F优先级");
        builder.Property(e => e.FIsDefault).HasColumnName("F是否默认分支");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30);
        builder.Property(e => e.FFailurePolicyJson).HasColumnName("F失败兜底JSON");

        builder.HasIndex(e => new { e.FFlowVersionId, e.FEdgeKey })
            .IsUnique()
            .HasDatabaseName("IX_CF流程条件边_版本边键");
        builder.HasIndex(e => new { e.FFlowVersionId, e.FFromStageKey, e.FPriority })
            .HasDatabaseName("IX_CF流程条件边_来源优先级");
    }
}
