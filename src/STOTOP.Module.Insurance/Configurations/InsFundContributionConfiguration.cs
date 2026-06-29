using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Insurance.Entities;

namespace STOTOP.Module.Insurance.Configurations;

public class InsFundContributionConfiguration : IEntityTypeConfiguration<InsFundContribution>
{
    public void Configure(EntityTypeBuilder<InsFundContribution> builder)
    {
        builder.ToTable("INS共保基金缴费");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FFundId).HasColumnName("F基金ID").IsRequired();
        builder.Property(e => e.FPolicyId).HasColumnName("F保单ID");
        builder.Property(e => e.FBusinessType).HasColumnName("F业务类型").IsRequired();
        builder.Property(e => e.FRelatedObjectId).HasColumnName("F关联对象ID").IsRequired();
        builder.Property(e => e.FRelatedObjectName).HasColumnName("F关联对象名称").HasMaxLength(200);
        builder.Property(e => e.FContributionNumber).HasColumnName("F缴费编号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FContributionAmount).HasColumnName("F缴费金额").HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.FPeriodStart).HasColumnName("F缴费周期开始").IsRequired();
        builder.Property(e => e.FPeriodEnd).HasColumnName("F缴费周期结束").IsRequired();
        builder.Property(e => e.FPaymentDate).HasColumnName("F缴费日期");
        builder.Property(e => e.FPaymentStatus).HasColumnName("F缴费状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FFundId)
            .HasDatabaseName("IX_INS共保基金缴费_基金ID");
        builder.HasIndex(e => new { e.FBusinessType, e.FRelatedObjectId })
            .HasDatabaseName("IX_INS共保基金缴费_业务关联");
    }
}
