using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfNotificationConfigConfiguration : IEntityTypeConfiguration<CfNotificationConfig>
{
    public void Configure(EntityTypeBuilder<CfNotificationConfig> builder)
    {
        builder.ToTable("CF通知配置");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FConfigKey).HasColumnName("F配置键").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FConfigValue).HasColumnName("F配置值");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F修改时间");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);

        builder.HasIndex(e => new { e.FOrgId, e.FConfigKey })
            .IsUnique()
            .HasDatabaseName("UX_CF通知配置_组织_键");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_CF通知配置_租户ID");
    }
}
