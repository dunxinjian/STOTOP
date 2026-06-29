using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmCustomerProfitConfiguration : IEntityTypeConfiguration<CrmCustomerProfit>
{
    public void Configure(EntityTypeBuilder<CrmCustomerProfit> builder)
    {
        builder.ToTable("CRM客户毛利");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCustomerId).HasColumnName("F客户ID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FPeriod).HasColumnName("F期间").HasMaxLength(10).IsRequired();
        builder.Property(e => e.FRevenue).HasColumnName("F收入").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FCost).HasColumnName("F成本").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FProfit).HasColumnName("F毛利").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FProfitRate).HasColumnName("F毛利率").HasColumnType("decimal(8,4)").HasDefaultValue(0m);
        builder.Property(e => e.FDataSource).HasColumnName("F数据来源").HasDefaultValue(1);
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FCustomerId).HasDatabaseName("IX_CRM客户毛利_F客户ID");
        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_CRM客户毛利_F组织ID");
        builder.HasIndex(e => new { e.FCustomerId, e.FPeriod }).IsUnique().HasDatabaseName("UX_CRM客户毛利_客户期间");
    }
}
