using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Configurations;

public class PmPointRuleConfiguration : IEntityTypeConfiguration<PmPointRule>
{
    public void Configure(EntityTypeBuilder<PmPointRule> builder)
    {
        builder.ToTable("PM积分规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FSourceId).HasColumnName("F来源ID");
        builder.Property(e => e.FRuleName).HasColumnName("F规则名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FRuleCode).HasColumnName("F规则编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FEventType).HasColumnName("F事件类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FPointValue).HasColumnName("F积分值");
        builder.Property(e => e.FConditionExpression).HasColumnName("F条件表达式").HasMaxLength(500);
        builder.Property(e => e.FConditionDescription).HasColumnName("F条件说明").HasMaxLength(200);
        builder.Property(e => e.FMultiplierRule).HasColumnName("F倍率规则").HasMaxLength(200);
        builder.Property(e => e.FCycleLimit).HasColumnName("F周期上限").HasDefaultValue(0);
        builder.Property(e => e.FRequireApproval).HasColumnName("F需要审批").HasDefaultValue(false);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").HasDefaultValue(0);
        builder.Property(e => e.FIsEnabled).HasColumnName("F是否启用").HasDefaultValue(true);
        builder.Property(e => e.F账户类型).HasColumnName("F账户类型").HasDefaultValue(2);
        builder.Property(e => e.F清算策略).HasColumnName("F清算策略").HasDefaultValue(0);
        builder.Property(e => e.F转换比例).HasColumnName("F转换比例").HasColumnType("decimal(18,4)").HasDefaultValue(1.0m);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.FRuleCode }).IsUnique().HasDatabaseName("UQ_PM积分规则_组织_编码");
        builder.HasIndex(e => e.FSourceId).HasDatabaseName("IX_PM积分规则_来源ID");
    }
}
