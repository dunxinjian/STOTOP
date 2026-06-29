using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Vehicle.Entities;

namespace STOTOP.Module.Vehicle.Configurations;

public class VehRentalStandardConfiguration : IEntityTypeConfiguration<VehRentalStandard>
{
    public void Configure(EntityTypeBuilder<VehRentalStandard> builder)
    {
        builder.ToTable("VEH租赁费用标准");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.FChargeCycle).HasColumnName("F收费周期").HasDefaultValue(1);
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期").IsRequired();
        builder.Property(e => e.FExpiryDate).HasColumnName("F失效日期");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FName).HasDatabaseName("IX_VEH租赁费用标准_名称");
    }
}
