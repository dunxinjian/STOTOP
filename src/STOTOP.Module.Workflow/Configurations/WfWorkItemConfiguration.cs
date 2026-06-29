using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Configurations;

public class WfWorkItemConfiguration : IEntityTypeConfiguration<WfWorkItem>
{
    public void Configure(EntityTypeBuilder<WfWorkItem> builder)
    {
        builder.ToTable("WF工作项");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(32).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(4000);
        builder.Property(e => e.FType).HasColumnName("F类型");
        builder.Property(e => e.FSource).HasColumnName("F来源");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FPriority).HasColumnName("F优先级").HasDefaultValue(1);
        builder.Property(e => e.FChainId).HasColumnName("F链路ID").HasMaxLength(64);
        builder.Property(e => e.FChainSeq).HasColumnName("F链路序号");
        builder.Property(e => e.FParentWorkItemId).HasColumnName("F父工作项ID");
        builder.Property(e => e.FDataScopeId).HasColumnName("F数据作用域ID").HasMaxLength(64);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FAssigneeId).HasColumnName("F处理人ID");
        builder.Property(e => e.FAssigneeName).HasColumnName("F处理人姓名").HasMaxLength(50);
        builder.Property(e => e.FModule).HasColumnName("F模块").HasMaxLength(50);
        builder.Property(e => e.FBizType).HasColumnName("F业务类型").HasMaxLength(50);
        builder.Property(e => e.FBizId).HasColumnName("F业务ID");
        builder.Property(e => e.FDetailRoute).HasColumnName("F详情路由").HasMaxLength(500);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间");
        builder.Property(e => e.FDeadline).HasColumnName("F截止时间");
        builder.Property(e => e.FCompletedTime).HasColumnName("F完成时间");
        builder.Property(e => e.FResolvedAt).HasColumnName("F解决时间");
        builder.Property(e => e.FCategory).HasColumnName("F分类").HasMaxLength(50);
        builder.Property(e => e.FIsOverdue).HasColumnName("F是否超时").HasDefaultValue(false);
        builder.Property(e => e.FClaimedAt).HasColumnName("F认领时间");
        builder.Property(e => e.FDispatchWarning).HasColumnName("F派发警告").HasMaxLength(500);

        builder.Property(e => e.FResult).HasColumnName("F处理结果");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(2000);

        // 索引
        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => e.FChainId).HasDatabaseName("IX_WF工作项_链路ID");
        builder.HasIndex(e => e.FAssigneeId).HasDatabaseName("IX_WF工作项_处理人ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_WF工作项_状态");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_WF工作项_数据作用域");
        builder.HasIndex(e => new { e.FModule, e.FBizType }).HasDatabaseName("IX_WF工作项_模块业务类型");
        builder.HasIndex(e => new { e.FCategory, e.FIsOverdue }).HasDatabaseName("IX_WF工作项_分类超时");

        // 关系
        builder.HasOne(e => e.ParentWorkItem)
            .WithMany(e => e.ChildWorkItems)
            .HasForeignKey(e => e.FParentWorkItemId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.Logs)
            .WithOne(e => e.WorkItem)
            .HasForeignKey(e => e.FWorkItemId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
