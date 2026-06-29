using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Vehicle.Entities;

namespace STOTOP.Module.Vehicle.Configurations;

public class VehMaintenanceConfiguration : IEntityTypeConfiguration<VehMaintenance>
{
    public void Configure(EntityTypeBuilder<VehMaintenance> builder)
    {
        builder.ToTable("VEH维修记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FVehicleId).HasColumnName("F车辆ID").IsRequired();
        builder.Property(e => e.FMaintenanceDate).HasColumnName("F维修日期").IsRequired();
        builder.Property(e => e.FMaintenanceType).HasColumnName("F维修类型").HasMaxLength(50);
        builder.Property(e => e.FMaintenanceItem).HasColumnName("F维修项目").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FMaintenanceUnit).HasColumnName("F维修单位").HasMaxLength(200);
        builder.Property(e => e.FMaintenanceCost).HasColumnName("F维修费用").HasPrecision(18, 2);
        builder.Property(e => e.FCostBearer).HasColumnName("F费用承担方").HasDefaultValue(1);
        builder.Property(e => e.FCompletionDate).HasColumnName("F完成日期");
        builder.Property(e => e.FMaintenanceStatus).HasColumnName("F维修状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FVehicleId).HasDatabaseName("IX_VEH维修记录_车辆ID");
        builder.HasIndex(e => e.FMaintenanceDate).HasDatabaseName("IX_VEH维修记录_维修日期");
    }
}
