using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfSystemDispatchResultConfiguration : IEntityTypeConfiguration<CfSystemDispatchResult>
{
    public void Configure(EntityTypeBuilder<CfSystemDispatchResult> builder)
    {
        builder.ToTable("CF系统派发结果");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID");
        builder.Property(e => e.FStagingTable).HasColumnName("F暂存表").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FDispatchRuleId).HasColumnName("F派发规则ID");
        builder.Property(e => e.FRuleName).HasColumnName("F规则名称").HasMaxLength(200);
        builder.Property(e => e.FSeverity).HasColumnName("F严重级别").HasMaxLength(20);
        builder.Property(e => e.FAffectedRowCount).HasColumnName("F命中行数").HasDefaultValue(0);
        builder.Property(e => e.FAffectedRowIds).HasColumnName("F命中行ID").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FProcessingStatus).HasColumnName("F处理状态").HasDefaultValue(0);
        builder.Property(e => e.FProcessingResult).HasColumnName("F处理结果").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FContext).HasColumnName("F上下文").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FBatchId).HasDatabaseName("IX_CF系统派发结果_批次");
        builder.HasIndex(e => e.FDispatchRuleId).HasDatabaseName("IX_CF系统派发结果_规则");
    }
}
