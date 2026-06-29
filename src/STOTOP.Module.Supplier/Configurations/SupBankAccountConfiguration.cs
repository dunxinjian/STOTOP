using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Supplier.Entities;

namespace STOTOP.Module.Supplier.Configurations;

public class SupBankAccountConfiguration : IEntityTypeConfiguration<SupBankAccount>
{
    public void Configure(EntityTypeBuilder<SupBankAccount> builder)
    {
        builder.ToTable("SUP供应商收款账户");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FSupplierId).HasColumnName("F供应商ID");
        builder.Property(e => e.FAccountName).HasColumnName("F账户名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FBankName).HasColumnName("F银行名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FBankAccountNumber).HasColumnName("F银行账号").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FBranchName).HasColumnName("F开户行").HasMaxLength(300);
        builder.Property(e => e.FIsDefault).HasColumnName("F是否默认").HasDefaultValue(false);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FSupplierId).HasDatabaseName("IX_SUP收款账户_供应商ID");
    }
}
