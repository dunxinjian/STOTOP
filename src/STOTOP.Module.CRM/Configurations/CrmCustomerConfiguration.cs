using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmCustomerConfiguration : IEntityTypeConfiguration<CrmCustomer>
{
    public void Configure(EntityTypeBuilder<CrmCustomer> builder)
    {
        builder.ToTable("CRM客户");

        builder.HasKey(e => e.FCode);
        builder.Property(e => e.FCode).HasColumnName("F编号").HasMaxLength(50);
        builder.Property(e => e.FServiceObjectId).HasColumnName("F业务对象ID").HasMaxLength(50);
        builder.Property(e => e.FShortName).HasColumnName("F简称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FFullName).HasColumnName("F全称").HasMaxLength(500);
        builder.Property(e => e.FContact).HasColumnName("F联系人").HasMaxLength(100);
        builder.Property(e => e.FPhone).HasColumnName("F电话").HasMaxLength(50);
        builder.Property(e => e.FIndustry).HasColumnName("F行业").HasMaxLength(100);
        builder.Property(e => e.FScale).HasColumnName("F规模").HasMaxLength(50);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FBdEmployeeId).HasColumnName("FBD员工ID");
        builder.Property(e => e.FMaintenanceEmployeeId).HasColumnName("F运维员工ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        // ===== 客户扩展属性（对应 EXP业务对象 F类型=1，原位于 EXP业务对象，2026-04 回归到此） =====
        builder.Property(e => e.FSenderAddress).HasColumnName("F发件地址").HasMaxLength(200);
        builder.Property(e => e.FOfficeAddress).HasColumnName("F办公地址").HasMaxLength(200);
        builder.Property(e => e.FCargoType).HasColumnName("F货物类型").HasMaxLength(50);
        builder.Property(e => e.FPrepayPerTicket).HasColumnName("F预充单价").HasPrecision(10, 2);
        builder.Property(e => e.FAttachmentPath).HasColumnName("F附件").HasMaxLength(500);
        builder.Property(e => e.FSourceClientType).HasColumnName("F客户类型原值").HasMaxLength(50);
        builder.Property(e => e.FSettlementModeText).HasColumnName("F结算方式").HasMaxLength(50);
        builder.Property(e => e.FWarehouseCategory).HasColumnName("F仓别").HasMaxLength(50);
        builder.Property(e => e.FCutoffTime).HasColumnName("F截单时间").HasMaxLength(50);
        builder.Property(e => e.FRequiredArea).HasColumnName("F客户所需面积").HasMaxLength(50);
        builder.Property(e => e.FDailyOrderVolume).HasColumnName("F日常单量").HasMaxLength(50);
        builder.Property(e => e.FSkuStructure).HasColumnName("F产品SKU结构").HasMaxLength(200);
        builder.Property(e => e.FCombinedProducts).HasColumnName("F组合商品").HasMaxLength(200);
        builder.Property(e => e.FPlatform).HasColumnName("F平台").HasMaxLength(100);
        builder.Property(e => e.FExpressPriority).HasColumnName("F快递优先级").HasMaxLength(50);
        builder.Property(e => e.FRemoteDelivery).HasColumnName("F偏远地区发货").HasMaxLength(50);
        builder.Property(e => e.FReturnRestock).HasColumnName("F退货重新上架").HasMaxLength(50);
        builder.Property(e => e.FCustomerSoftware).HasColumnName("F客户使用软件").HasMaxLength(100);
        builder.Property(e => e.FTempClientId).HasColumnName("F客户临时ID").HasMaxLength(50);

        builder.HasIndex(e => e.FServiceObjectId).HasDatabaseName("IX_CRM客户_F业务对象ID");
        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_CRM客户_F组织ID");
        builder.HasIndex(e => e.FBdEmployeeId).HasDatabaseName("IX_CRM客户_FBD员工ID");
        builder.HasIndex(e => e.FMaintenanceEmployeeId).HasDatabaseName("IX_CRM客户_F运维员工ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_CRM客户_F状态");

        builder.HasMany(e => e.Contacts)
            .WithOne(e => e.Customer)
            .HasForeignKey(e => e.FCustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.Transfers)
            .WithOne(e => e.Customer)
            .HasForeignKey(e => e.FCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.VisitRecords)
            .WithOne(e => e.Customer)
            .HasForeignKey(e => e.FCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.ServiceOrders)
            .WithOne(e => e.Customer)
            .HasForeignKey(e => e.FCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Profits)
            .WithOne(e => e.Customer)
            .HasForeignKey(e => e.FCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Accounts)
            .WithOne(e => e.Customer)
            .HasForeignKey(e => e.FCustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
