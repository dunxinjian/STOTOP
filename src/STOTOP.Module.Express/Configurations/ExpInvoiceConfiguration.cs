using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpInvoiceConfiguration : IEntityTypeConfiguration<ExpInvoice>
{
    public void Configure(EntityTypeBuilder<ExpInvoice> builder)
    {
        builder.ToTable("EXP出港账单");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FInvoiceNo).HasColumnName("F账单号").HasMaxLength(30);
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FQuotationId).HasColumnName("F报价ID");
        builder.Property(e => e.FClientType).HasColumnName("F业务对象类型").HasMaxLength(2);
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FNetworkPointId).HasColumnName("F网点ID");
        builder.Property(e => e.FPeriodStart).HasColumnName("F账期开始");
        builder.Property(e => e.FPeriodEnd).HasColumnName("F账期结束");
        builder.Property(e => e.FTotalWaybills).HasColumnName("F总单量");
        builder.Property(e => e.FTotalWeight).HasColumnName("F总重量").HasPrecision(14, 3);
        builder.Property(e => e.FAvgWeight).HasColumnName("F平均重量").HasPrecision(10, 3);
        builder.Property(e => e.FWeightCap).HasColumnName("F均重上限").HasPrecision(10, 3);
        builder.Property(e => e.FExcessWeight).HasColumnName("F超出均重").HasPrecision(10, 3);
        builder.Property(e => e.FWeightCapSurcharge).HasColumnName("F均重追补").HasPrecision(12, 2);
        builder.Property(e => e.FQuotaSurcharge).HasColumnName("F占比追补").HasPrecision(12, 2);
        builder.Property(e => e.FTotalCharge).HasColumnName("F总应收").HasPrecision(14, 2);
        builder.Property(e => e.FTotalChargeWithSurcharge).HasColumnName("F含追补应收").HasPrecision(14, 2);
        builder.Property(e => e.FTotalCost).HasColumnName("F总成本").HasPrecision(14, 2);
        builder.Property(e => e.FTotalProfit).HasColumnName("F总利润").HasPrecision(14, 2);
        builder.Property(e => e.FPrepayDeduction).HasColumnName("F预付抵扣").HasPrecision(14, 2);
        builder.Property(e => e.FPayableAmount).HasColumnName("F应付金额").HasPrecision(14, 2);
        builder.Property(e => e.FReviewStatus).HasColumnName("F审核状态").HasDefaultValue(0);
        builder.Property(e => e.FReviewer).HasColumnName("F审核人").HasMaxLength(50);
        builder.Property(e => e.FReviewTime).HasColumnName("F审核时间");
        builder.Property(e => e.FReviewRemark).HasColumnName("F审核备注").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FArchived).HasColumnName("F已归档").HasDefaultValue(false);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_EXP出港账单_租户ID");
        builder.Property(e => e.FArchivedTime).HasColumnName("F归档时间");
        builder.Property(e => e.FArchivedBy).HasColumnName("F归档人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        // 对账字段
        builder.Property(e => e.FReconciliationStatus).HasColumnName("F对账状态").HasDefaultValue(0);
        builder.Property(e => e.FReconciliationRemarks).HasColumnName("F对账备注").HasMaxLength(500);
        builder.Property(e => e.FReconciliationBy).HasColumnName("F对账人ID");
        builder.Property(e => e.FReconciliationTime).HasColumnName("F对账时间");
        builder.Property(e => e.FDisputeReason).HasColumnName("F异议原因");
        builder.Property(e => e.FDisputeResolvedBy).HasColumnName("F异议处理人ID");
        builder.Property(e => e.FDisputeResolvedTime).HasColumnName("F异议处理时间");
        builder.Property(e => e.FDisputeResolution).HasColumnName("F异议处理说明").HasMaxLength(500);

        builder.HasIndex(e => e.FInvoiceNo).IsUnique()
            .HasFilter("[F账单号] IS NOT NULL")
            .HasDatabaseName("IX_EXP出港账单_F账单号");
        builder.HasIndex(e => new { e.FBusinessObjectId, e.FPeriodEnd })
            .HasDatabaseName("IX_EXP出港账单_业务对象账期");
        builder.HasIndex(e => new { e.FReviewStatus, e.FArchived })
            .HasDatabaseName("IX_EXP出港账单_审核归档");
        builder.HasIndex(e => new { e.FNetworkPointId, e.FBusinessObjectId, e.FPeriodStart })
            .HasDatabaseName("IX_EXP出港账单_网点业务对象账期");
    }
}
