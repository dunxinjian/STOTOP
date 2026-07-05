using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAccountSetRuleConfiguration : IEntityTypeConfiguration<FinAccountSetRule>
{
    public void Configure(EntityTypeBuilder<FinAccountSetRule> builder)
    {
        builder.ToTable("FIN账套规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FRequireAuditSeparation).HasColumnName("F制单审核分离").HasDefaultValue(false);
        builder.Property(e => e.FProfitAccountCode).HasColumnName("F本年利润科目编码").HasMaxLength(20);
        builder.Property(e => e.FRetainedAccountCode).HasColumnName("F未分配利润科目编码").HasMaxLength(20);
        builder.Property(e => e.FEnabledVoucherWords).HasColumnName("F启用凭证字");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FAccountSetId).IsUnique().HasDatabaseName("IX_FIN账套规则_账套ID");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_FIN账套规则_租户ID");
    }
}
