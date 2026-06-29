using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpInvoiceReviewLogConfiguration : IEntityTypeConfiguration<ExpInvoiceReviewLog>
{
    public void Configure(EntityTypeBuilder<ExpInvoiceReviewLog> builder)
    {
        builder.ToTable("EXP出港账单审核日志");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FInvoiceId).HasColumnName("F账单ID");
        builder.Property(e => e.FAction).HasColumnName("F操作");
        builder.Property(e => e.FRuleId).HasColumnName("F规则ID");
        builder.Property(e => e.FRuleResult).HasColumnName("F规则结果").HasMaxLength(500);
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => e.FInvoiceId)
            .HasDatabaseName("IX_EXP出港账单审核日志_F账单ID");
    }
}
