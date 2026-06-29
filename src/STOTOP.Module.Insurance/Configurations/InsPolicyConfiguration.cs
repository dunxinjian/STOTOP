using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Insurance.Entities;

namespace STOTOP.Module.Insurance.Configurations;

public class InsPolicyConfiguration : IEntityTypeConfiguration<InsPolicy>
{
    public void Configure(EntityTypeBuilder<InsPolicy> builder)
    {
        builder.ToTable("INS保单");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FBusinessType).HasColumnName("F业务类型").IsRequired();
        builder.Property(e => e.FRelatedObjectId).HasColumnName("F关联对象ID").IsRequired();
        builder.Property(e => e.FRelatedObjectName).HasColumnName("F关联对象名称").HasMaxLength(200);
        builder.Property(e => e.FInsuranceCategory).HasColumnName("F保险大类").IsRequired();
        builder.Property(e => e.FInsuranceType).HasColumnName("F保险类型").HasMaxLength(100);
        builder.Property(e => e.FInsuranceCompanyId).HasColumnName("F保险公司ID");
        builder.Property(e => e.FPolicyNumber).HasColumnName("F保单号").HasMaxLength(100);
        builder.Property(e => e.FPremium).HasColumnName("F保费").HasPrecision(18, 2);
        builder.Property(e => e.FInsuredAmount).HasColumnName("F保额").HasPrecision(18, 2);
        builder.Property(e => e.FContactPerson).HasColumnName("F联系人").HasMaxLength(50);
        builder.Property(e => e.FContactPhone).HasColumnName("F联系电话").HasMaxLength(50);
        builder.Property(e => e.FCoInsuranceFundId).HasColumnName("F共保基金ID");
        builder.Property(e => e.FParticipationNumber).HasColumnName("F参保编号").HasMaxLength(100);
        builder.Property(e => e.FPaymentCycle).HasColumnName("F缴费周期");
        builder.Property(e => e.FPerPeriodAmount).HasColumnName("F每期金额").HasPrecision(18, 2);
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期").IsRequired();
        builder.Property(e => e.FExpiryDate).HasColumnName("F到期日期").IsRequired();
        builder.Property(e => e.FInsuranceStatus).HasColumnName("F保险状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FBusinessType, e.FRelatedObjectId })
            .HasDatabaseName("IX_INS保单_业务关联");
        builder.HasIndex(e => e.FInsuranceCompanyId)
            .HasDatabaseName("IX_INS保单_保险公司");
        builder.HasIndex(e => e.FExpiryDate)
            .HasDatabaseName("IX_INS保单_到期日期");

        builder.HasMany(e => e.Claims)
            .WithOne(e => e.Policy)
            .HasForeignKey(e => e.FPolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Settlements)
            .WithOne(e => e.Policy)
            .HasForeignKey(e => e.FPolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.FundContributions)
            .WithOne(e => e.Policy)
            .HasForeignKey(e => e.FPolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
