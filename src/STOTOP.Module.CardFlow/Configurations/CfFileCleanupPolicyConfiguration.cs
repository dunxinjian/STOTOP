using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfFileCleanupPolicyConfiguration : IEntityTypeConfiguration<CfFileCleanupPolicy>
{
    public void Configure(EntityTypeBuilder<CfFileCleanupPolicy> builder)
    {
        builder.ToTable("CF文件清理策略");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FPolicyName).HasColumnName("F策略名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FRetentionDays).HasColumnName("F保留天数");
        builder.Property(e => e.FCronExpression).HasColumnName("FCron表达式").HasMaxLength(100);
        builder.Property(e => e.FHangfireJobId).HasColumnName("FHangfireJobId").HasMaxLength(100);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FLastExecutedTime).HasColumnName("F上次执行时间");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");
    }
}
