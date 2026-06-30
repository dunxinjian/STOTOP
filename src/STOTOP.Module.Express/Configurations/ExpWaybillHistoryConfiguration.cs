using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpWaybillHistoryConfiguration : IEntityTypeConfiguration<ExpWaybillHistory>
{
    public void Configure(EntityTypeBuilder<ExpWaybillHistory> builder)
    {
        builder.ToTable("EXP出港运单_历史");

        builder.Property(e => e.FID).HasColumnName("FID").ValueGeneratedNever();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_EXP出港运单_历史_租户ID");
        builder.Property(e => e.FWaybillNo).HasColumnName("F运单编号").HasMaxLength(30).IsRequired();
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FShopName).HasColumnName("F店铺名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50);
        builder.Property(e => e.FSenderProvince).HasColumnName("F寄件省").HasMaxLength(20);
        builder.Property(e => e.FReceiverProvinceId).HasColumnName("F目的省份ID");
        builder.Property(e => e.FPickupWeight).HasColumnName("F揽收重量").HasPrecision(10, 3);
        builder.Property(e => e.FTransitWeight).HasColumnName("F中转重量").HasPrecision(10, 3);
        builder.Property(e => e.FDeliveryWeight).HasColumnName("F到件重量").HasPrecision(10, 3);
        builder.Property(e => e.FBagWeight).HasColumnName("F集包重量").HasPrecision(10, 3);
        builder.Property(e => e.FBubbleWeight).HasColumnName("F计泡重量").HasPrecision(10, 3);
        builder.Property(e => e.FHqWeight).HasColumnName("F总部重量").HasPrecision(10, 3);
        builder.Property(e => e.FIsDirectDelivery).HasColumnName("F一单到底");
        builder.Property(e => e.FActualWeight).HasColumnName("F结算实重").HasPrecision(10, 3);
        builder.Property(e => e.FLength).HasColumnName("F长").HasPrecision(10, 1);
        builder.Property(e => e.FWidth).HasColumnName("F宽").HasPrecision(10, 1);
        builder.Property(e => e.FHeight).HasColumnName("F高").HasPrecision(10, 1);
        builder.Property(e => e.FVolumetricWeight).HasColumnName("F抛重").HasPrecision(10, 3)
            .ValueGeneratedOnAddOrUpdate();
        builder.Property(e => e.FBillableWeight).HasColumnName("F计费重量").HasPrecision(10, 3);
        builder.Property(e => e.FDeclaredValue).HasColumnName("F声明价值").HasPrecision(12, 2);
        builder.Property(e => e.FWaybillDate).HasColumnName("F运单日期");
        builder.Property(e => e.FImportBatchId).HasColumnName("F导入批次ID");
        builder.Property(e => e.FClientAlias).HasColumnName("F客户别名").HasMaxLength(50);
        builder.Property(e => e.FBillingStatus).HasColumnName("F计费状态").HasDefaultValue(0);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FArchivedAt).HasColumnName("F归档时间");

        builder.HasIndex(e => new { e.FWaybillNo, e.FBrandCode })
            .HasDatabaseName("IX_EXP出港运单_历史_运单号品牌");
        builder.HasIndex(e => e.FArchivedAt)
            .HasDatabaseName("IX_EXP出港运单_历史_归档时间");
    }
}
