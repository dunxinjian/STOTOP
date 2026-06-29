using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmTaskScheduleConfiguration : IEntityTypeConfiguration<TmTaskSchedule>
{
    public void Configure(EntityTypeBuilder<TmTaskSchedule> builder)
    {
        builder.ToTable("TM任务调度");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTemplateTaskId).HasColumnName("F模板任务ID");
        builder.Property(e => e.FScheduleType).HasColumnName("F调度类型");
        builder.Property(e => e.FCronExpression).HasColumnName("FCron表达式").HasMaxLength(100);
        builder.Property(e => e.FScheduledTime).HasColumnName("F定时执行");
        builder.Property(e => e.FNextExecution).HasColumnName("F下次执行");
        builder.Property(e => e.FLastExecution).HasColumnName("F上次执行");
        builder.Property(e => e.FIsEnabled).HasColumnName("F是否启用").HasDefaultValue(true);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FNextExecution, e.FIsEnabled })
            .HasDatabaseName("IX_TM任务调度_下次执行_是否启用");

        builder.HasOne(e => e.TemplateTask)
            .WithMany()
            .HasForeignKey(e => e.FTemplateTaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
