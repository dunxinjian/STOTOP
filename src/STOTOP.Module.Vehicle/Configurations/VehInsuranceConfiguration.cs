using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Vehicle.Entities;

namespace STOTOP.Module.Vehicle.Configurations;

public class VehInsuranceConfiguration : IEntityTypeConfiguration<VehInsurance>
{
    public void Configure(EntityTypeBuilder<VehInsurance> builder)
    {
        builder.ToTable("VEH保险记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FVehicleId).HasColumnName("F车辆ID");
        builder.Property(e => e.FInsuranceType).HasColumnName("F保险类型").HasMaxLength(50);
        builder.Property(e => e.FInsuranceCompany).HasColumnName("F保险公司").HasMaxLength(200);
        builder.Property(e => e.FPolicyNo).HasColumnName("F保单号").HasMaxLength(100);
        builder.Property(e => e.FPremium).HasColumnName("F保费").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期");
        builder.Property(e => e.FExpiryDate).HasColumnName("F到期日期");
        builder.Property(e => e.FInsuranceStatus).HasColumnName("F保险状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0);

        builder.HasIndex(e => e.FPolicyNo).HasDatabaseName("IX_VEH保险记录_保单号");
        builder.HasIndex(e => e.FVehicleId).HasDatabaseName("IX_VEH保险记录_车辆ID");
        builder.HasIndex(e => e.FExpiryDate).HasDatabaseName("IX_VEH保险记录_到期日期");
    }
}
