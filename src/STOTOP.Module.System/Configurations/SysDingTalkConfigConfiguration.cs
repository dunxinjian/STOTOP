using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysDingTalkConfigConfiguration : IEntityTypeConfiguration<SysDingTalkConfig>
{
    public void Configure(EntityTypeBuilder<SysDingTalkConfig> builder)
    {
        builder.ToTable("SYS钉钉配置");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FConfigName).HasColumnName("F配置名称").HasMaxLength(100);
        builder.Property(e => e.FCorpId).HasColumnName("F企业CorpId").HasMaxLength(100);
        builder.Property(e => e.FAppKey).HasColumnName("FAppKey").HasMaxLength(100);
        builder.Property(e => e.FAppSecret).HasColumnName("FAppSecret").HasMaxLength(500);
        builder.Property(e => e.FAgentId).HasColumnName("FAgentId").HasMaxLength(100);
        builder.Property(e => e.FDomain).HasColumnName("F自定义域名").HasMaxLength(200);
        builder.Property(e => e.FRobotWebhookUrl).HasColumnName("F群机器人Webhook").HasMaxLength(500);
        builder.Property(e => e.FRobotSecret).HasColumnName("F群机器人Secret").HasMaxLength(200);
        builder.Property(e => e.FIsEnabled).HasColumnName("F是否启用").HasDefaultValue(1);
        builder.Property(e => e.FAutoSync).HasColumnName("F自动同步").HasDefaultValue(0);
        builder.Property(e => e.FSyncCron).HasColumnName("F同步Cron").HasMaxLength(50);
        builder.Property(e => e.FLastSyncTime).HasColumnName("F最后同步时间");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        // 每租户一套配置
        builder.HasIndex(e => e.FTenantId).IsUnique().HasDatabaseName("UQ_SYS钉钉配置_租户ID");
    }
}
