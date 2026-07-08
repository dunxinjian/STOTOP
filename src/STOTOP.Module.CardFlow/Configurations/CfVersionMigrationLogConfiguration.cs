using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfVersionMigrationLogConfiguration : IEntityTypeConfiguration<CfVersionMigrationLog>
{
    public void Configure(EntityTypeBuilder<CfVersionMigrationLog> builder)
    {
        builder.ToTable("CF版本迁移日志");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FFlowDefinitionId).HasColumnName("F定义ID");
        builder.Property(e => e.FCardId).HasColumnName("F卡片ID");
        builder.Property(e => e.FOldVersionId).HasColumnName("F旧版本ID");
        builder.Property(e => e.FNewVersionId).HasColumnName("F新版本ID");
        builder.Property(e => e.FOldStageKey).HasColumnName("F旧节点Key").HasMaxLength(80);
        builder.Property(e => e.FNewStageKey).HasColumnName("F新节点Key").HasMaxLength(80);
        builder.Property(e => e.FResult).HasColumnName("F结果").HasMaxLength(30);
        builder.Property(e => e.FMessage).HasColumnName("F消息").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => e.FFlowDefinitionId).HasDatabaseName("IX_CF版本迁移日志_定义");
        builder.HasIndex(e => e.FCardId).HasDatabaseName("IX_CF版本迁移日志_卡片");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_CF版本迁移日志_租户ID");
    }
}
