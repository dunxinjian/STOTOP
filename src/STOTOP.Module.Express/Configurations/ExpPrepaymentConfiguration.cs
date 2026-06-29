using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPrepaymentConfiguration : IEntityTypeConfiguration<ExpPrepayment>
{
    public void Configure(EntityTypeBuilder<ExpPrepayment> builder)
    {
        builder.ToTable("EXP预付款记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasPrecision(14, 2);
        builder.Property(e => e.FPaymentDate).HasColumnName("F付款日期");
        builder.Property(e => e.FPaymentMethod).HasColumnName("F付款方式").HasMaxLength(50);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
    }
}
