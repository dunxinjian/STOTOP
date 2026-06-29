using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmActivityLogConfiguration : IEntityTypeConfiguration<TmActivityLog>
{
    public void Configure(EntityTypeBuilder<TmActivityLog> builder)
    {
        builder.ToTable("TM活动日志");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID");
        builder.Property(e => e.FActionType).HasColumnName("F动作类型");
        builder.Property(e => e.FOldValue).HasColumnName("F原值").HasMaxLength(200);
        builder.Property(e => e.FNewValue).HasColumnName("F新值").HasMaxLength(200);
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FTaskId).HasDatabaseName("IX_TM活动日志_任务ID");
    }
}
