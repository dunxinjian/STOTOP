using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Insurance.Entities;

namespace STOTOP.Module.Insurance.Configurations;

public class InsSettlementConfiguration : IEntityTypeConfiguration<InsSettlement>
{
    public void Configure(EntityTypeBuilder<InsSettlement> builder)
    {
        builder.ToTable("INS理赔记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FClaimId).HasColumnName("F出险ID").IsRequired();
        builder.Property(e => e.FPolicyId).HasColumnName("F保单ID").IsRequired();
        builder.Property(e => e.FSettlementNumber).HasColumnName("F理赔编号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FSettlementType).HasColumnName("F理赔类型").IsRequired();
        builder.Property(e => e.FApplyDate).HasColumnName("F申请日期").IsRequired();
        builder.Property(e => e.FApplicantId).HasColumnName("F申请人ID");
        builder.Property(e => e.FApplicantName).HasColumnName("F申请人姓名").HasMaxLength(100);
        builder.Property(e => e.FAssessedAmount).HasColumnName("F定损金额").HasPrecision(18, 2);
        builder.Property(e => e.FSettlementAmount).HasColumnName("F理赔金额").HasPrecision(18, 2);
        builder.Property(e => e.FSelfPayAmount).HasColumnName("F自付金额").HasPrecision(18, 2);
        builder.Property(e => e.FDeductible).HasColumnName("F免赔额").HasPrecision(18, 2);
        builder.Property(e => e.FSettlementStatus).HasColumnName("F理赔状态").HasDefaultValue(1);
        builder.Property(e => e.FCurrentStepId).HasColumnName("F当前环节ID");
        builder.Property(e => e.FPaymentDate).HasColumnName("F赔付日期");
        builder.Property(e => e.FPaymentVoucher).HasColumnName("F赔付凭证").HasMaxLength(500);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FSettlementNumber)
            .IsUnique()
            .HasDatabaseName("IX_INS理赔记录_编号");
        builder.HasIndex(e => e.FClaimId)
            .HasDatabaseName("IX_INS理赔记录_出险ID");
        builder.HasIndex(e => e.FSettlementStatus)
            .HasDatabaseName("IX_INS理赔记录_状态");

        builder.HasOne(e => e.CurrentStep)
            .WithMany()
            .HasForeignKey(e => e.FCurrentStepId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.ApprovalRecords)
            .WithOne(e => e.Settlement)
            .HasForeignKey(e => e.FSettlementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
