using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmWaybillPoolConfiguration : IEntityTypeConfiguration<CrmWaybillPool>
{
    public void Configure(EntityTypeBuilder<CrmWaybillPool> builder)
    {
        builder.ToTable("CRM号段池");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FPrefix).HasColumnName("F号段前缀").HasMaxLength(50);
        builder.Property(e => e.FStartNo).HasColumnName("F起始号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FEndNo).HasColumnName("F结束号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FTotalCount).HasColumnName("F总量").HasDefaultValue(0);
        builder.Property(e => e.FAllocatedCount).HasColumnName("F已发放").HasDefaultValue(0);
        builder.Property(e => e.FRemainingCount).HasColumnName("F剩余").HasDefaultValue(0);
        builder.Property(e => e.FPurchaseDate).HasColumnName("F采购日期");
        builder.Property(e => e.FUnitPrice).HasColumnName("F单价").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FVersion).HasColumnName("F版本号").HasDefaultValue(0).IsConcurrencyToken();
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FBrandCode).HasDatabaseName("IX_CRM号段池_F品牌编码");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_CRM号段池_F状态");
    }
}
