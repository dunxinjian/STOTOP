using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfDownloadTaskConfiguration : IEntityTypeConfiguration<CfDownloadTask>
{
    public void Configure(EntityTypeBuilder<CfDownloadTask> builder)
    {
        builder.ToTable("CF下载任务");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTaskName).HasColumnName("F任务名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FTargetUrl).HasColumnName("F目标网站").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FLoginAccount).HasColumnName("F登录账号").HasMaxLength(100);
        builder.Property(e => e.FLoginPassword).HasColumnName("F登录密码").HasMaxLength(200);
        builder.Property(e => e.FScriptConfig).HasColumnName("F脚本配置");
        builder.Property(e => e.FFilterConfig).HasColumnName("F筛选条件");
        builder.Property(e => e.FStoragePath).HasColumnName("F存储路径").HasMaxLength(500);
        builder.Property(e => e.FCronExpression).HasColumnName("FCron表达式").HasMaxLength(100);
        builder.Property(e => e.FHangfireJobId).HasColumnName("FHangfireJobId").HasMaxLength(100);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
    }
}
