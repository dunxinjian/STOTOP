using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfPluginRuleHitStatConfiguration : IEntityTypeConfiguration<CfPluginRuleHitStat>
{
    public void Configure(EntityTypeBuilder<CfPluginRuleHitStat> builder)
    {
        builder.ToTable("CF自动插件_规则命中统计");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId");
        builder.Property(e => e.FRuleId).HasColumnName("F规则ID");
        builder.Property(e => e.FRuleGroupIndex).HasColumnName("F规则组序号");
        builder.Property(e => e.FEntryLineNo).HasColumnName("F分录行号");
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID");
        builder.Property(e => e.FHitRowCount).HasColumnName("F命中行数");
        builder.Property(e => e.FMissRowCount).HasColumnName("F未命中行数");
        builder.Property(e => e.FStatTime).HasColumnName("F统计时间");
        builder.Property(e => e.FInvalidated).HasColumnName("F已失效").HasDefaultValue(false);

        builder.HasIndex(e => new { e.FRuleId, e.FRuleGroupIndex, e.FEntryLineNo, e.FBatchId })
            .IsUnique()
            .HasDatabaseName("UX_CF自动插件_规则命中统计_规则批次");

        builder.HasIndex(e => new { e.FRuleId, e.FInvalidated, e.FStatTime })
            .HasDatabaseName("IX_CF自动插件_规则命中统计_查询");
    }
}
