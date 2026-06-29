using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfPickupTaskConfiguration : IEntityTypeConfiguration<ConfPickupTask>
{
    public void Configure(EntityTypeBuilder<ConfPickupTask> builder)
    {
        builder.ToTable("CONF接送任务");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEventId).HasColumnName("F活动ID");
        builder.Property(e => e.FVehicleId).HasColumnName("F车辆ID");
        builder.Property(e => e.FType).HasColumnName("F类型").HasMaxLength(20);
        builder.Property(e => e.FDate).HasColumnName("F日期").HasColumnType("date");
        builder.Property(e => e.FTime).HasColumnName("F时间").HasColumnType("time");
        builder.Property(e => e.FOrigin).HasColumnName("F出发地").HasMaxLength(200);
        builder.Property(e => e.FDestination).HasColumnName("F目的地").HasMaxLength(200);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(20).HasDefaultValue("待安排");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(200);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasOne(e => e.Vehicle)
            .WithMany(e => e.PickupTasks)
            .HasForeignKey(e => e.FVehicleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.FEventId).HasDatabaseName("IX_CONF接送任务_活动ID");
        builder.HasIndex(e => new { e.FDate, e.FTime }).HasDatabaseName("IX_CONF接送任务_日期时间");
    }
}
