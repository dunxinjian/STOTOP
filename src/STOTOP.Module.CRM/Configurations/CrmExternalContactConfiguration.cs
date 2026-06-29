using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmExternalContactConfiguration : IEntityTypeConfiguration<CrmExternalContact>
{
    public void Configure(EntityTypeBuilder<CrmExternalContact> builder)
    {
        builder.ToTable("CRM外部联系人");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F姓名").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FPhone).HasColumnName("F电话").HasMaxLength(50);
        builder.Property(e => e.FCompany).HasColumnName("F公司").HasMaxLength(200);
        builder.Property(e => e.FBankAccount).HasColumnName("F收款账户").HasMaxLength(100);
        builder.Property(e => e.FBankName).HasColumnName("F开户行").HasMaxLength(200);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
    }
}
