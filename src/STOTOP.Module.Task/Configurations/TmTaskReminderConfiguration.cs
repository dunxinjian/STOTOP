using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmTaskReminderConfiguration : IEntityTypeConfiguration<TmTaskReminder>
{
    public void Configure(EntityTypeBuilder<TmTaskReminder> builder)
    {
        builder.ToTable("TM任务提醒");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FReminderTime).HasColumnName("F提醒时间");
        builder.Property(e => e.FReminderType).HasColumnName("F提醒类型");
        builder.Property(e => e.FIsRead).HasColumnName("F已读").HasDefaultValue(false);
        builder.Property(e => e.FIsSent).HasColumnName("F已发送").HasDefaultValue(false);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FReminderTime, e.FIsSent })
            .HasDatabaseName("IX_TM任务提醒_提醒时间_已发送");
    }
}
