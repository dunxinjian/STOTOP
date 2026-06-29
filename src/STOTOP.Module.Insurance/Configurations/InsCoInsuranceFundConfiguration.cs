using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Insurance.Entities;

namespace STOTOP.Module.Insurance.Configurations;

public class InsCoInsuranceFundConfiguration : IEntityTypeConfiguration<InsCoInsuranceFund>
{
    public void Configure(EntityTypeBuilder<InsCoInsuranceFund> builder)
    {
        builder.ToTable("INS共保基金");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FFundName).HasColumnName("F基金名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FFundCode).HasColumnName("F基金编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FBusinessType).HasColumnName("F业务类型").HasDefaultValue(1);
        builder.Property(e => e.FFundDescription).HasColumnName("F基金说明").HasMaxLength(1000);
        builder.Property(e => e.FTotalContributions).HasColumnName("F累计缴费").HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(e => e.FTotalPayouts).HasColumnName("F累计赔付").HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(e => e.FFundBalance).HasColumnName("F基金余额").HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(e => e.FContributionStandard).HasColumnName("F缴费标准").HasPrecision(18, 2);
        builder.Property(e => e.FPaymentCycle).HasColumnName("F缴费周期");
        builder.Property(e => e.FDeductible).HasColumnName("F免赔额").HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(e => e.FSinglePayoutLimit).HasColumnName("F单次赔付上限").HasPrecision(18, 2);
        builder.Property(e => e.FAnnualPayoutLimit).HasColumnName("F年度赔付上限").HasPrecision(18, 2);
        builder.Property(e => e.FFundStatus).HasColumnName("F基金状态").HasDefaultValue(1);
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期").IsRequired();
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FOrgId, e.FFundCode })
            .IsUnique()
            .HasDatabaseName("IX_INS共保基金_编码");

        builder.HasMany(e => e.Policies)
            .WithOne(e => e.CoInsuranceFund)
            .HasForeignKey(e => e.FCoInsuranceFundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Contributions)
            .WithOne(e => e.Fund)
            .HasForeignKey(e => e.FFundId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
