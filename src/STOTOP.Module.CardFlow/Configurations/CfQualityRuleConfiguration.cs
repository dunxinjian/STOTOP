using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfQualityRuleConfiguration : IEntityTypeConfiguration<CfQualityRule>
{
    public void Configure(EntityTypeBuilder<CfQualityRule> builder)
    {
        builder.ToTable("CF质量规则");

        builder.Property(e => e.FID).HasColumnName("FID").ValueGeneratedOnAdd();
        builder.Property(e => e.FRuleName).HasColumnName("F规则名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FRuleCode).HasColumnName("F规则编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FTargetTable).HasColumnName("F目标表名").HasMaxLength(100);
        builder.Property(e => e.FRuleLevel).HasColumnName("F规则级别").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FCheckType).HasColumnName("F检查类型").HasMaxLength(30).IsRequired();
        builder.Property(e => e.FTargetField).HasColumnName("F目标字段").HasMaxLength(100);
        builder.Property(e => e.FRuleParamsJson).HasColumnName("F规则参数JSON");
        builder.Property(e => e.FErrorCode).HasColumnName("F错误类型编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FSeverityLevel).HasColumnName("F严重级别").HasMaxLength(10).IsRequired();
        builder.Property(e => e.FQualityDimension).HasColumnName("F质量维度").HasMaxLength(30);
        builder.Property(e => e.FErrorMessageTemplate).HasColumnName("F错误消息模板").HasMaxLength(500);
        builder.Property(e => e.FSuggestedFix).HasColumnName("F建议修复方案").HasMaxLength(500);
        builder.Property(e => e.FIsBlocking).HasColumnName("F是否阻断").HasDefaultValue(false);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").HasDefaultValue(0);
        builder.Property(e => e.FEnabled).HasColumnName("F是否启用").HasDefaultValue(true);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FTargetTable, e.FOrgId })
            .HasDatabaseName("IX_CF质量规则_目标表组织");
        builder.HasIndex(e => e.FRuleCode)
            .IsUnique()
            .HasDatabaseName("IX_CF质量规则_编码唯一");
    }
}
