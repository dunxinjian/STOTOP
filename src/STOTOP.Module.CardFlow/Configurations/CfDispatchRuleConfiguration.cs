using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfDispatchRuleConfiguration : IEntityTypeConfiguration<CfDispatchRule>
{
    public void Configure(EntityTypeBuilder<CfDispatchRule> builder)
    {
        builder.ToTable("CF派发规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FRuleName).HasColumnName("F规则名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FTriggerEvent).HasColumnName("F触发事件").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FTargetTables).HasColumnName("F适用暂存表").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FRuleType).HasColumnName("F规则类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FConditionJson).HasColumnName("F条件定义").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FSeverity).HasColumnName("F严重级别").HasMaxLength(20).HasDefaultValue("Info");
        builder.Property(e => e.FHandlerType).HasColumnName("F处理器类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FHandlerConfigJson).HasColumnName("F处理器配置").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FIsAsync).HasColumnName("F异步执行").HasDefaultValue(true);
        builder.Property(e => e.FPriority).HasColumnName("F优先级").HasDefaultValue(100);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FDescription).HasColumnName("F说明").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
    }
}
