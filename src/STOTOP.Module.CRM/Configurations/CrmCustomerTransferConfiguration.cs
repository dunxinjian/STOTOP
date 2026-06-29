using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmCustomerTransferConfiguration : IEntityTypeConfiguration<CrmCustomerTransfer>
{
    public void Configure(EntityTypeBuilder<CrmCustomerTransfer> builder)
    {
        builder.ToTable("CRM客户流转记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCustomerId).HasColumnName("F客户ID").HasMaxLength(50);
        builder.Property(e => e.FTransferType).HasColumnName("F流转类型");
        builder.Property(e => e.FOriginalOrgId).HasColumnName("F原组织ID");
        builder.Property(e => e.FNewOrgId).HasColumnName("F新组织ID");
        builder.Property(e => e.FOriginalBdEmployeeId).HasColumnName("F原BD员工ID");
        builder.Property(e => e.FNewBdEmployeeId).HasColumnName("F新BD员工ID");
        builder.Property(e => e.FOriginalStatus).HasColumnName("F原状态");
        builder.Property(e => e.FNewStatus).HasColumnName("F新状态");
        builder.Property(e => e.FReason).HasColumnName("F原因").HasMaxLength(500);
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FCustomerId).HasDatabaseName("IX_CRM客户流转记录_F客户ID");
        builder.HasIndex(e => e.FOperatorId).HasDatabaseName("IX_CRM客户流转记录_F操作人ID");
    }
}
