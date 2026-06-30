using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpQuotationConfiguration : IEntityTypeConfiguration<ExpQuotation>
{
    public void Configure(EntityTypeBuilder<ExpQuotation> builder)
    {
        builder.ToTable("EXP快递报价");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_EXP快递报价_租户ID");
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FPlanCode).HasColumnName("F方案编号").HasMaxLength(50);
        builder.Property(e => e.FPlanName).HasColumnName("F方案名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FNetworkPointCode).HasColumnName("F网点编号").HasMaxLength(50);
        builder.Property(e => e.FClientType).HasColumnName("F业务对象类型").HasMaxLength(2);
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象编号").HasMaxLength(50);
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期");
        builder.Property(e => e.FSettlementWeightStage).HasColumnName("F结算重量环节").HasDefaultValue(1);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        // 商务条款
        builder.Property(e => e.FPaymentMode).HasColumnName("F付款方式").HasDefaultValue(2);
        // 精度 (6,4)：UI 按百分比录入后 ÷100 存小数，0.1% 粒度需要 4 位小数
        // （(5,2) 会把 12.5% → 0.125 四舍五入成 0.13，保存后回显漂移为 13%）
        builder.Property(e => e.FPrepayRatio).HasColumnName("F预付比例").HasPrecision(6, 4);
        builder.Property(e => e.FBillingCycle).HasColumnName("F账单周期").HasDefaultValue(2);
        builder.Property(e => e.FBillingDay).HasColumnName("F出账日");
        builder.Property(e => e.FPaymentDueDay).HasColumnName("F付款截止日");
        builder.Property(e => e.FThrowRatio).HasColumnName("F抛比").HasDefaultValue(8000);
        builder.Property(e => e.FInsuranceRate).HasColumnName("F保价费率").HasPrecision(5, 4);
        builder.Property(e => e.FOaProcessId).HasColumnName("FOA流程ID");
        builder.Property(e => e.FApprovedBy).HasColumnName("F审批人").HasMaxLength(100);
        builder.Property(e => e.FApprovedAt).HasColumnName("F审批时间");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(2000);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        builder.Property(e => e.FSharedShopEnabled).HasColumnName("F共享店铺");
        builder.Property(e => e.F上一版本ID).HasColumnName("F上一版本ID");
        builder.Property(e => e.F含税).HasColumnName("F含税");
        builder.Property(e => e.F源FID).HasColumnName("F源FID");
        builder.Property(e => e.F版本).HasColumnName("F版本").HasDefaultValue(1);
        builder.Property(e => e.F税率).HasColumnName("F税率").HasPrecision(5, 4);
        builder.Property(e => e.F重量进位方式).HasColumnName("F重量进位方式");
        builder.Property(e => e.FMatrixJson).HasColumnName("F矩阵JSON");

        builder.HasMany(e => e.Shops)
            .WithOne(s => s.Quotation)
            .HasForeignKey(s => s.FQuotationId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.Aliases)
            .WithOne(a => a.Quotation)
            .HasForeignKey(a => a.FQuotationId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Commission)
            .WithOne(c => c.Quotation)
            .HasForeignKey<ExpQuotationCommission>(c => c.FQuotationId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(e => e.FPlanCode).IsUnique()
            .HasFilter("[F方案编号] IS NOT NULL")
            .HasDatabaseName("IX_EXP快递报价_方案编号");
        builder.HasIndex(e => new { e.FBrandCode, e.FStatus })
            .HasDatabaseName("IX_EXP快递报价_品牌状态");
    }
}
