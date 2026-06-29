using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmTaskConfiguration : IEntityTypeConfiguration<TmTask>
{
    public void Configure(EntityTypeBuilder<TmTask> builder)
    {
        builder.ToTable("TM任务");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(32).IsRequired();
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FProjectId).HasColumnName("F项目ID");
        builder.Property(e => e.FGoalId).HasColumnName("F目标ID");
        builder.Property(e => e.FKRId).HasColumnName("FKRID");
        builder.Property(e => e.FParentTaskId).HasColumnName("F父任务ID").HasDefaultValue(0L);
        builder.Property(e => e.FType).HasColumnName("F类型").HasDefaultValue(0);
        builder.Property(e => e.FPriority).HasColumnName("F优先级").HasDefaultValue(2);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FAssigneeId).HasColumnName("F执行人ID");
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FPlanStart).HasColumnName("F计划开始");
        builder.Property(e => e.FPlanEnd).HasColumnName("F计划截止");
        builder.Property(e => e.FActualStart).HasColumnName("F实际开始");
        builder.Property(e => e.FActualEnd).HasColumnName("F实际完成");
        builder.Property(e => e.FEstimatedHours).HasColumnName("F预估工时").HasColumnType("decimal(10,1)");
        builder.Property(e => e.FActualHours).HasColumnName("F实际工时").HasColumnType("decimal(10,1)");
        builder.Property(e => e.FProgress).HasColumnName("F进度").HasDefaultValue(0);
        builder.Property(e => e.FVisibility).HasColumnName("F可见范围").HasDefaultValue(0);
        builder.Property(e => e.FIsTemplate).HasColumnName("F是模板").HasDefaultValue(false);
        builder.Property(e => e.FCode).HasColumnName("F编号").HasMaxLength(20);
        builder.Property(e => e.FSort).HasColumnName("F排序").HasDefaultValue(0);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => e.FProjectId).HasDatabaseName("IX_TM任务_项目ID");
        builder.HasIndex(e => e.FGoalId).HasDatabaseName("IX_TM任务_目标ID");
        builder.HasIndex(e => e.FKRId).HasDatabaseName("IX_TM任务_KRID");
        builder.HasIndex(e => e.FParentTaskId).HasDatabaseName("IX_TM任务_父任务ID");
        builder.HasIndex(e => e.FAssigneeId).HasDatabaseName("IX_TM任务_执行人ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_TM任务_状态");
        builder.HasIndex(e => e.FCode).HasDatabaseName("IX_TM任务_编号");

        builder.HasMany(e => e.Children)
            .WithOne()
            .HasForeignKey(e => e.FParentTaskId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.Members)
            .WithOne(e => e.Task)
            .HasForeignKey(e => e.FTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Tags)
            .WithOne(e => e.Task)
            .HasForeignKey(e => e.FTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Comments)
            .WithOne(e => e.Task)
            .HasForeignKey(e => e.FTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.ActivityLogs)
            .WithOne(e => e.Task)
            .HasForeignKey(e => e.FTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Reminders)
            .WithOne(e => e.Task)
            .HasForeignKey(e => e.FTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.ProgressReports)
            .WithOne(e => e.Task)
            .HasForeignKey(e => e.FTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.DingTalkTodos)
            .WithOne(e => e.Task)
            .HasForeignKey(e => e.FTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Dependencies)
            .WithOne(e => e.Task)
            .HasForeignKey(e => e.FTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.VisibilityRules)
            .WithOne(e => e.Task)
            .HasForeignKey(e => e.FTaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
