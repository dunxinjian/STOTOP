using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Configurations;

public class WfDispatchRuleConfiguration : IEntityTypeConfiguration<WfDispatchRule>
{
    public void Configure(EntityTypeBuilder<WfDispatchRule> builder)
    {
        builder.ToTable("WF派发规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(32).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(2000);
        builder.Property(e => e.FModule).HasColumnName("F模块").HasMaxLength(50);
        builder.Property(e => e.FBizType).HasColumnName("F业务类型").HasMaxLength(50);
        builder.Property(e => e.FDispatchMode).HasColumnName("F派发模式");
        builder.Property(e => e.FAutoAssignRule).HasColumnName("F自动分配规则");
        builder.Property(e => e.FTimeout).HasColumnName("F超时时间").HasDefaultValue(0);
        builder.Property(e => e.FEscalationRule).HasColumnName("F升级规则");
        builder.Property(e => e.FPriority).HasColumnName("F优先级").HasDefaultValue(0);
        builder.Property(e => e.FIsEnabled).HasColumnName("F是否启用").HasDefaultValue(true);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间");

        // 索引
        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => new { e.FModule, e.FBizType }).HasDatabaseName("IX_WF派发规则_模块业务类型");
        builder.HasIndex(e => e.FIsEnabled).HasDatabaseName("IX_WF派发规则_启用状态");
    }
}
