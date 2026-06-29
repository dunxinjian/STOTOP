using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlRuleConfiguration : IEntityTypeConfiguration<QlRule>
{
    public void Configure(EntityTypeBuilder<QlRule> builder)
    {
        builder.ToTable("QL检测规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRuleName).HasColumnName("F规则名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FBusinessLine).HasColumnName("F业务线").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FConditionExpression).HasColumnName("F条件表达式").HasMaxLength(500);
        builder.Property(e => e.FDispatchMethod).HasColumnName("F派发方式");
        builder.Property(e => e.FDispatchTarget).HasColumnName("F派发目标").HasMaxLength(200);
        builder.Property(e => e.FDefaultPriority).HasColumnName("F默认优先级");
        builder.Property(e => e.FTimeoutHours).HasColumnName("F超时小时数");
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FOrgId, e.FBusinessLine }).HasDatabaseName("IX_QL检测规则_组织_业务线");
    }
}
