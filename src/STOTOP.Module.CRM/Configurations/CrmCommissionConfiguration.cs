using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmCommissionConfiguration : IEntityTypeConfiguration<CrmCommission>
{
    public void Configure(EntityTypeBuilder<CrmCommission> builder)
    {
        builder.ToTable("CRM返佣申请");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FReferralId).HasColumnName("F推荐记录ID");
        builder.Property(e => e.FCustomerId).HasColumnName("F客户ID").HasMaxLength(50);
        builder.Property(e => e.FContractId).HasColumnName("F合同ID");
        builder.Property(e => e.FCommissionAmount).HasColumnName("F返佣金额").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FCalcBasis).HasColumnName("F计算依据").HasMaxLength(500);
        builder.Property(e => e.FApplicantId).HasColumnName("F申请人ID");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FOaProcessInstanceId).HasColumnName("FOA流程实例ID");
        builder.Property(e => e.FPaymentOrderId).HasColumnName("F付款单ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FReferralId).HasDatabaseName("IX_CRM返佣申请_F推荐记录ID");
        builder.HasIndex(e => e.FCustomerId).HasDatabaseName("IX_CRM返佣申请_F客户ID");
        builder.HasIndex(e => e.FContractId).HasDatabaseName("IX_CRM返佣申请_F合同ID");
        builder.HasIndex(e => e.FApplicantId).HasDatabaseName("IX_CRM返佣申请_F申请人ID");

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.FCustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
