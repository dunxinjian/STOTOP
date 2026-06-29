using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmCustomerAccountConfiguration : IEntityTypeConfiguration<CrmCustomerAccount>
{
    public void Configure(EntityTypeBuilder<CrmCustomerAccount> builder)
    {
        builder.ToTable("CRM客户账户");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCustomerId).HasColumnName("F客户ID").HasMaxLength(50);
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FBalance).HasColumnName("F余额").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FTotalRecharge).HasColumnName("F累计充值").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FTotalConsumption).HasColumnName("F累计消费").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FFrozenAmount).HasColumnName("F冻结金额").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FCustomerId).HasDatabaseName("IX_CRM客户账户_F客户ID");
        builder.HasIndex(e => e.FBrandCode).HasDatabaseName("IX_CRM客户账户_F品牌编码");
        builder.HasIndex(e => new { e.FCustomerId, e.FBrandCode }).IsUnique().HasDatabaseName("UX_CRM客户账户_客户品牌");

        builder.HasMany(e => e.Prepayments)
            .WithOne(e => e.CustomerAccount)
            .HasForeignKey(e => e.FCustomerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
