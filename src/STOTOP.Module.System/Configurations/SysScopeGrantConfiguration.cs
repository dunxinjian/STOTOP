using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysScopeGrantConfiguration : IEntityTypeConfiguration<SysScopeGrant>
{
    public void Configure(EntityTypeBuilder<SysScopeGrant> builder)
    {
        builder.ToTable("SYS数据范围授权");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FScopeType).HasColumnName("F范围类型");
        builder.Property(e => e.FScopeNodeId).HasColumnName("F范围节点ID");
        builder.Property(e => e.FScopeAction).HasColumnName("F范围动作").HasDefaultValue((int)ScopeAction.Read);
        builder.Property(e => e.FGrantSource).HasColumnName("F授权来源").HasDefaultValue((int)ScopeGrantSource.Derived);
        builder.Property(e => e.FApprovalId).HasColumnName("F审批单ID");
        builder.Property(e => e.FExpireAt).HasColumnName("F到期时间");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FUserId, e.FTenantId }).HasDatabaseName("IX_SYS数据范围授权_用户_租户");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_SYS数据范围授权_租户ID");
    }
}
