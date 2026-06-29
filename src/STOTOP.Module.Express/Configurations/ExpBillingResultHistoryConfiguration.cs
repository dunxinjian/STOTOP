using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpBillingResultHistoryConfiguration : IEntityTypeConfiguration<ExpBillingResultHistory>
{
    public void Configure(EntityTypeBuilder<ExpBillingResultHistory> builder)
    {
        builder.ToTable("EXP出港运单_计费结果_历史");

        builder.Property(e => e.FID).HasColumnName("FID").ValueGeneratedNever();
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID");
        builder.Property(e => e.FWaybillNo).HasColumnName("F运单编号").HasMaxLength(50);
        builder.Property(e => e.FWaybillDate).HasColumnName("F运单日期");
        builder.Property(e => e.FPartyClientId).HasColumnName("F业务对象编号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FPartyRole).HasColumnName("F参与方角色");
        builder.Property(e => e.FChainLevel).HasColumnName("F层级");
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FBillingDate).HasColumnName("F计费日期");
        builder.Property(e => e.FBillableWeight).HasColumnName("F计费重量").HasPrecision(10, 3);
        builder.Property(e => e.FFreightCharge).HasColumnName("F基础运费").HasPrecision(12, 2);
        builder.Property(e => e.FInsuranceFee).HasColumnName("F保价费").HasPrecision(12, 2);
        builder.Property(e => e.FSurchargeAmount).HasColumnName("F加收费用").HasPrecision(12, 2);
        builder.Property(e => e.FWaiverAmount).HasColumnName("F减免金额").HasPrecision(12, 2);
        builder.Property(e => e.FCommissionAmount).HasColumnName("F佣金金额").HasPrecision(12, 2);
        builder.Property(e => e.FChargeAmount).HasColumnName("F应收金额").HasPrecision(12, 2);
        builder.Property(e => e.FClientType).HasColumnName("F业务对象类型").HasMaxLength(2);
        builder.Property(e => e.FQuotationCode).HasColumnName("F报价编号").HasMaxLength(50);
        builder.Property(e => e.FQuotationId).HasColumnName("F报价ID");
        builder.Property(e => e.FCommissionRuleId).HasColumnName("F佣金规则ID");
        builder.Property(e => e.FCalcStatus).HasColumnName("F计算状态").HasDefaultValue(1);
        builder.Property(e => e.FErrorMessage).HasColumnName("F异常信息").HasMaxLength(200);
        builder.Property(e => e.FInvoiceId).HasColumnName("F账单ID");
        builder.Property(e => e.FDestinationProvinceId).HasColumnName("F目的省份ID");
        builder.Property(e => e.FDestProvinceName).HasColumnName("F目的省份").HasMaxLength(20);
        builder.Property(e => e.FNetworkPointCode).HasColumnName("F归属网点编号").HasMaxLength(50);
        builder.Property(e => e.FTotalCost).HasColumnName("F成本合计").HasColumnType("DECIMAL(12,2)");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FArchivedAt).HasColumnName("F归档时间");

        builder.HasIndex(e => e.FArchivedAt)
            .HasDatabaseName("IX_EXP出港运单_计费结果_历史_归档时间");
    }
}
