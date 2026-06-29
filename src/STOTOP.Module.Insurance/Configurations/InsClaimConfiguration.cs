using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Insurance.Entities;

namespace STOTOP.Module.Insurance.Configurations;

public class InsClaimConfiguration : IEntityTypeConfiguration<InsClaim>
{
    public void Configure(EntityTypeBuilder<InsClaim> builder)
    {
        builder.ToTable("INS出险记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FPolicyId).HasColumnName("F保单ID");
        builder.Property(e => e.FBusinessType).HasColumnName("F业务类型").IsRequired();
        builder.Property(e => e.FRelatedObjectId).HasColumnName("F关联对象ID").IsRequired();
        builder.Property(e => e.FRelatedObjectName).HasColumnName("F关联对象名称").HasMaxLength(200);
        builder.Property(e => e.FClaimNumber).HasColumnName("F出险编号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FClaimDate).HasColumnName("F出险日期").IsRequired();
        builder.Property(e => e.FClaimLocation).HasColumnName("F出险地点").HasMaxLength(500);
        builder.Property(e => e.FAccidentType).HasColumnName("F事故类型").IsRequired();
        builder.Property(e => e.FAccidentDescription).HasColumnName("F事故描述").HasMaxLength(2000);
        builder.Property(e => e.FCounterpartyInfo).HasColumnName("F对方信息").HasMaxLength(500);
        builder.Property(e => e.FEstimatedLoss).HasColumnName("F预估损失金额").HasPrecision(18, 2);
        builder.Property(e => e.FActualLoss).HasColumnName("F实际损失金额").HasPrecision(18, 2);
        builder.Property(e => e.FLiabilityDivision).HasColumnName("F责任划分");
        builder.Property(e => e.FPartyId).HasColumnName("F当事人ID");
        builder.Property(e => e.FPartyName).HasColumnName("F当事人姓名").HasMaxLength(100);
        builder.Property(e => e.FCaseNumber).HasColumnName("F报案号").HasMaxLength(100);
        builder.Property(e => e.FClaimImages).HasColumnName("F出险图片").HasMaxLength(2000);
        builder.Property(e => e.FClaimStatus).HasColumnName("F出险状态").HasDefaultValue(1);
        builder.Property(e => e.FClosedDate).HasColumnName("F结案日期");
        builder.Property(e => e.FClosedRemark).HasColumnName("F结案说明").HasMaxLength(1000);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FClaimNumber)
            .IsUnique()
            .HasDatabaseName("IX_INS出险记录_编号");
        builder.HasIndex(e => new { e.FBusinessType, e.FRelatedObjectId })
            .HasDatabaseName("IX_INS出险记录_业务关联");
        builder.HasIndex(e => e.FClaimStatus)
            .HasDatabaseName("IX_INS出险记录_状态");

        builder.HasMany(e => e.Settlements)
            .WithOne(e => e.Claim)
            .HasForeignKey(e => e.FClaimId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
