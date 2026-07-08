using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfDefinitionEditLockConfiguration : IEntityTypeConfiguration<CfDefinitionEditLock>
{
    public void Configure(EntityTypeBuilder<CfDefinitionEditLock> builder)
    {
        builder.ToTable("CF定义编辑锁");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FFlowDefinitionId).HasColumnName("F定义ID");
        builder.Property(e => e.FHolderId).HasColumnName("F持锁人ID");
        builder.Property(e => e.FHolderName).HasColumnName("F持锁人姓名").HasMaxLength(80);
        builder.Property(e => e.FAcquiredTime).HasColumnName("F获取时间");
        builder.Property(e => e.FHeartbeatAt).HasColumnName("F心跳时间");
        builder.Property(e => e.FTakeoverRequesterId).HasColumnName("F接管申请人ID");
        builder.Property(e => e.FTakeoverRequesterName).HasColumnName("F接管申请人姓名").HasMaxLength(80);
        builder.Property(e => e.FTakeoverRequestedAt).HasColumnName("F接管申请时间");

        // 单定义至多一锁：F定义ID 唯一索引
        builder.HasIndex(e => e.FFlowDefinitionId).IsUnique().HasDatabaseName("UX_CF定义编辑锁_定义");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_CF定义编辑锁_租户ID");
    }
}
