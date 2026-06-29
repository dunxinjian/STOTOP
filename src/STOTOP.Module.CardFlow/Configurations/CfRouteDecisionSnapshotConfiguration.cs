using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfRouteDecisionSnapshotConfiguration : IEntityTypeConfiguration<CfRouteDecisionSnapshot>
{
    public void Configure(EntityTypeBuilder<CfRouteDecisionSnapshot> builder)
    {
        builder.ToTable("CF流转决策快照");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCardId).HasColumnName("F卡片ID");
        builder.Property(e => e.FSourceStageInstanceId).HasColumnName("F来源节点实例ID");
        builder.Property(e => e.FFromStageDefinitionId).HasColumnName("F来源节点定义ID");
        builder.Property(e => e.FFromStageKey).HasColumnName("F来源节点键").HasMaxLength(80);
        builder.Property(e => e.FSelectedRouteRuleId).HasColumnName("F命中规则ID");
        builder.Property(e => e.FSelectedEdgeKey).HasColumnName("F命中边键").HasMaxLength(100);
        builder.Property(e => e.FToStageDefinitionId).HasColumnName("F目标节点定义ID");
        builder.Property(e => e.FToStageKey).HasColumnName("F目标节点键").HasMaxLength(80);
        builder.Property(e => e.FCandidateResultsJson).HasColumnName("F候选结果JSON");
        builder.Property(e => e.FDecisionSnapshotJson).HasColumnName("F决策快照JSON");
        builder.Property(e => e.FReason).HasColumnName("F决策原因").HasMaxLength(500);
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FDecisionTime).HasColumnName("F决策时间");
        builder.Property(e => e.FRound).HasColumnName("F轮次");

        builder.HasIndex(e => new { e.FCardId, e.FRound }).HasDatabaseName("IX_CF流转决策快照_卡片轮次");
        builder.HasIndex(e => e.FSourceStageInstanceId).HasDatabaseName("IX_CF流转决策快照_来源节点实例");
    }
}
