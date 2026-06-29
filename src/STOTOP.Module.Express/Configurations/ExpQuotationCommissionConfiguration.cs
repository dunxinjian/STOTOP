using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpQuotationCommissionConfiguration : IEntityTypeConfiguration<ExpQuotationCommission>
{
    public void Configure(EntityTypeBuilder<ExpQuotationCommission> builder)
    {
        builder.ToTable("EXP快递报价_佣金配置");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FQuotationId).HasColumnName("F报价ID");
        builder.Property(e => e.FEnabled).HasColumnName("F启用");
        builder.Property(e => e.FCalcMethod).HasColumnName("F计算方式").HasDefaultValue(1);
        builder.Property(e => e.FRate).HasColumnName("F费率").HasPrecision(5, 4);
        builder.Property(e => e.FFixedAmount).HasColumnName("F固定金额").HasPrecision(12, 2);
        builder.Property(e => e.FWeightAmount).HasColumnName("F单位重量金额").HasPrecision(12, 4);
        builder.Property(e => e.FTargetClientType).HasColumnName("F关联业务对象类型").HasMaxLength(2).IsRequired();
        builder.Property(e => e.FTargetClientId).HasColumnName("F关联业务对象ID").HasMaxLength(50);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasOne(e => e.Quotation)
            .WithOne(q => q.Commission)
            .HasForeignKey<ExpQuotationCommission>(e => e.FQuotationId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
