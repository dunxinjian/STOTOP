using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPrepaymentTransactionConfiguration : IEntityTypeConfiguration<ExpPrepaymentTransaction>
{
    public void Configure(EntityTypeBuilder<ExpPrepaymentTransaction> builder)
    {
        builder.ToTable("EXP预付款流水");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FTransactionType).HasColumnName("F交易类型");
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasPrecision(14, 2);
        builder.Property(e => e.FInvoiceId).HasColumnName("F账单ID");
        builder.Property(e => e.FBalanceAfter).HasColumnName("F交易后余额").HasPrecision(14, 2);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FBusinessObjectId, e.FCreatedTime })
            .HasDatabaseName("IX_EXP预付款流水_业务对象创建时间");
    }
}
