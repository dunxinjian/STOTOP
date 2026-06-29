using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinTreasuryAccountBindingConfiguration : IEntityTypeConfiguration<FinTreasuryAccountBinding>
{
    public void Configure(EntityTypeBuilder<FinTreasuryAccountBinding> builder)
    {
        builder.ToTable("FIN资金账户绑定");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FChannelId).HasColumnName("F交易渠道ID");
        builder.Property(e => e.FCashAccountId).HasColumnName("F现金银行科目ID");
        builder.Property(e => e.FAccountNo).HasColumnName("F账号").HasMaxLength(100);
        builder.Property(e => e.FOpeningSource).HasColumnName("F期初来源").HasMaxLength(50).HasDefaultValue("account_balance");
        builder.Property(e => e.FManualOpeningAmount).HasColumnName("F手工期初金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FAccountSetId, e.FOrgId, e.FChannelId })
            .HasDatabaseName("IX_FIN资金账户绑定_账套组织渠道");
    }
}
