using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfAdHocDispatchConfigConfiguration : IEntityTypeConfiguration<CfAdHocDispatchConfig>
{
    public void Configure(EntityTypeBuilder<CfAdHocDispatchConfig> builder)
    {
        builder.ToTable("CF自由派发配置");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FSourceFlowCode).HasColumnName("F源流程编码").HasMaxLength(50);
        builder.Property(e => e.FTargetFlowCode).HasColumnName("F目标流程编码").HasMaxLength(50);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FDataProtocolJson).HasColumnName("F数据协议JSON");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FIsEnabled).HasColumnName("F是否启用");
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => new { e.FSourceFlowCode, e.FTargetFlowCode, e.FOrgId })
            .IsUnique()
            .HasDatabaseName("IX_CF自由派发配置_源目标组织");
        builder.HasIndex(e => new { e.FSourceFlowCode, e.FOrgId, e.FIsEnabled })
            .HasDatabaseName("IX_CF自由派发配置_源编码查询");
    }
}
