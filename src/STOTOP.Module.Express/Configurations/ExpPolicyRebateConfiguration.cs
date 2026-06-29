using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPolicyRebateConfiguration : IEntityTypeConfiguration<ExpPolicyRebate>
{
    public void Configure(EntityTypeBuilder<ExpPolicyRebate> builder)
    {
        builder.ToTable("EXP政策返利");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FPolicyName).HasColumnName("F方案名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FRebateMode).HasColumnName("F返利模式");
        builder.Property(e => e.FFlatRebateAmount).HasColumnName("F通票返利金额").HasColumnType("decimal(10,4)");
        builder.Property(e => e.FSettlementCycle).HasColumnName("F结算周期").HasDefaultValue(3);
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期");
        builder.Property(e => e.FExpiryDate).HasColumnName("F失效日期");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FBrandCode, e.FStatus, e.FEffectiveDate })
            .HasDatabaseName("IX_EXP政策返利_品牌状态生效");
    }
}
