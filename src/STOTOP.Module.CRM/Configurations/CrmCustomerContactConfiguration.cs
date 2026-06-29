using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmCustomerContactConfiguration : IEntityTypeConfiguration<CrmCustomerContact>
{
    public void Configure(EntityTypeBuilder<CrmCustomerContact> builder)
    {
        builder.ToTable("CRM客户联系人");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCustomerId).HasColumnName("F客户ID").HasMaxLength(50);
        builder.Property(e => e.FName).HasColumnName("F姓名").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FPhone).HasColumnName("F电话").HasMaxLength(50);
        builder.Property(e => e.FPosition).HasColumnName("F职务").HasMaxLength(100);
        builder.Property(e => e.FRoleTag).HasColumnName("F角色标签").HasMaxLength(100);
        builder.Property(e => e.FIsPrimary).HasColumnName("F是否主联系人").HasDefaultValue(false);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FCustomerId).HasDatabaseName("IX_CRM客户联系人_F客户ID");
    }
}
