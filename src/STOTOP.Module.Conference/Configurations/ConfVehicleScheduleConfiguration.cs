using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfVehicleScheduleConfiguration : IEntityTypeConfiguration<ConfVehicleSchedule>
{
    public void Configure(EntityTypeBuilder<ConfVehicleSchedule> builder)
    {
        builder.ToTable("CONF车辆日程");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEventId).HasColumnName("F活动ID");
        builder.Property(e => e.FVehicleId).HasColumnName("F车辆ID");
        builder.Property(e => e.FDate).HasColumnName("F日期").HasColumnType("date");
        builder.Property(e => e.FStartTime).HasColumnName("F开始时间").HasColumnType("time");
        builder.Property(e => e.FEndTime).HasColumnName("F结束时间").HasColumnType("time");
        builder.Property(e => e.FTaskType).HasColumnName("F任务类型").HasMaxLength(20);
        builder.Property(e => e.FPickupTaskId).HasColumnName("F接送任务ID");
        builder.Property(e => e.FOrigin).HasColumnName("F出发地").HasMaxLength(200);
        builder.Property(e => e.FDestination).HasColumnName("F目的地").HasMaxLength(200);
        builder.Property(e => e.FPassengerCount).HasColumnName("F乘客人数");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(200);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasOne(e => e.Vehicle)
            .WithMany(e => e.VehicleSchedules)
            .HasForeignKey(e => e.FVehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PickupTask)
            .WithMany(e => e.VehicleSchedules)
            .HasForeignKey(e => e.FPickupTaskId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.FVehicleId, e.FDate }).HasDatabaseName("IX_CONF车辆日程_车辆日期");
        builder.HasIndex(e => e.FEventId).HasDatabaseName("IX_CONF车辆日程_活动ID");
    }
}
