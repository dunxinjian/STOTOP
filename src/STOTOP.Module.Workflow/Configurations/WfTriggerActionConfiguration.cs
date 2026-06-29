using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Configurations;

public class WfTriggerActionConfiguration : IEntityTypeConfiguration<WfTriggerAction>
{
    public void Configure(EntityTypeBuilder<WfTriggerAction> builder)
    {
        builder.ToTable("WF触发动作");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FKey).HasColumnName("F标识").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FLabel).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FIcon).HasColumnName("F图标").HasMaxLength(50);
        builder.Property(e => e.FModule).HasColumnName("F模块").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FRoute).HasColumnName("F路由").HasMaxLength(300).IsRequired();
        builder.Property(e => e.FCategory).HasColumnName("F类别").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FRequiredPermission).HasColumnName("F权限码").HasMaxLength(100);
        builder.Property(e => e.FOrder).HasColumnName("F排序");
        builder.Property(e => e.FIsEnabled).HasColumnName("F启用");
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间");

        builder.HasIndex(e => e.FKey).IsUnique().HasDatabaseName("IX_WF触发动作_标识");
        builder.HasIndex(e => e.FModule).HasDatabaseName("IX_WF触发动作_模块");
    }
}
