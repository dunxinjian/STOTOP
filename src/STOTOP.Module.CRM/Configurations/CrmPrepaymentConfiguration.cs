using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmPrepaymentConfiguration : IEntityTypeConfiguration<CrmPrepayment>
{
    public void Configure(EntityTypeBuilder<CrmPrepayment> builder)
    {
        builder.ToTable("CRM预付款记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCustomerId).HasColumnName("F客户ID").HasMaxLength(50);
        builder.Property(e => e.FCustomerAccountId).HasColumnName("F客户账户ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FPrepayAmount).HasColumnName("F预付金额").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FReceivedAmount).HasColumnName("F到账金额").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FExpectedWaybillCount).HasColumnName("F应发运单数").HasDefaultValue(0);
        builder.Property(e => e.FAllocatedWaybillCount).HasColumnName("F已发运单数").HasDefaultValue(0);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FBankTransactionId).HasColumnName("F银行流水ID");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FCustomerId).HasDatabaseName("IX_CRM预付款记录_F客户ID");
        builder.HasIndex(e => e.FCustomerAccountId).HasDatabaseName("IX_CRM预付款记录_F客户账户ID");
        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_CRM预付款记录_F组织ID");
        builder.HasIndex(e => e.FBrandCode).HasDatabaseName("IX_CRM预付款记录_F品牌编码");
        builder.HasIndex(e => e.FBankTransactionId).HasDatabaseName("IX_CRM预付款记录_F银行流水ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_CRM预付款记录_F状态");

        builder.HasMany(e => e.Allocations)
            .WithOne(e => e.Prepayment)
            .HasForeignKey(e => e.FPrepaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
