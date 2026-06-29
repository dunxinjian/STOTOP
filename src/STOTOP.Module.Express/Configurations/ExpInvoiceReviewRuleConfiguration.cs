using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpInvoiceReviewRuleConfiguration : IEntityTypeConfiguration<ExpInvoiceReviewRule>
{
    public void Configure(EntityTypeBuilder<ExpInvoiceReviewRule> builder)
    {
        builder.ToTable("EXP出港账单审核规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FRuleName).HasColumnName("F规则名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FRuleType).HasColumnName("F规则类型");
        builder.Property(e => e.FMinValue).HasColumnName("F最小值").HasPrecision(14, 2);
        builder.Property(e => e.FMaxValue).HasColumnName("F最大值").HasPrecision(14, 2);
        builder.Property(e => e.FThreshold).HasColumnName("F阈值").HasPrecision(14, 4);
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50);
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FPriority).HasColumnName("F优先级").HasDefaultValue(0);
        builder.Property(e => e.FEnabled).HasColumnName("F启用").HasDefaultValue(true);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
    }
}
