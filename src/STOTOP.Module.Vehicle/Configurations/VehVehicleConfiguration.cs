using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Vehicle.Entities;

namespace STOTOP.Module.Vehicle.Configurations;

public class VehVehicleConfiguration : IEntityTypeConfiguration<VehVehicle>
{
    public void Configure(EntityTypeBuilder<VehVehicle> builder)
    {
        builder.ToTable("VEH车辆台账");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FPlateNumber).HasColumnName("F车牌号").HasMaxLength(20);
        builder.Property(e => e.FBrand).HasColumnName("F品牌").HasMaxLength(100);
        builder.Property(e => e.FFrameNumber).HasColumnName("F车架号").HasMaxLength(100);
        builder.Property(e => e.FOwnershipType).HasColumnName("F权属类型").HasDefaultValue(1);
        builder.Property(e => e.FOwnerId).HasColumnName("F所有人ID");
        builder.Property(e => e.FOwnerName).HasColumnName("F所有人姓名").HasMaxLength(100);
        builder.Property(e => e.FPurchaseDate).HasColumnName("F购入日期");
        builder.Property(e => e.FPurchasePrice).HasColumnName("F购入价格").HasPrecision(18, 2);
        builder.Property(e => e.FVehicleStatus).HasColumnName("F车辆状态").HasDefaultValue(1);
        builder.Property(e => e.FColor).HasColumnName("F颜色").HasMaxLength(20);
        builder.Property(e => e.FGpsDeviceNo).HasColumnName("FGPS设备号").HasMaxLength(50);
        builder.Property(e => e.FImage).HasColumnName("F图片").HasMaxLength(500);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("IX_VEH车辆台账_编码");
        builder.HasIndex(e => e.FPlateNumber).HasDatabaseName("IX_VEH车辆台账_车牌号");

        builder.HasMany(e => e.Assignments)
            .WithOne(e => e.Vehicle)
            .HasForeignKey(e => e.FVehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Maintenances)
            .WithOne(e => e.Vehicle)
            .HasForeignKey(e => e.FVehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
