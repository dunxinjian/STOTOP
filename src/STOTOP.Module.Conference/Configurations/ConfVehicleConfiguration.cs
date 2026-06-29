using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfVehicleConfiguration : IEntityTypeConfiguration<ConfVehicle>
{
    public void Configure(EntityTypeBuilder<ConfVehicle> builder)
    {
        builder.ToTable("CONF车辆");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEventId).HasColumnName("F活动ID");
        builder.Property(e => e.FPlateNumber).HasColumnName("F车牌号").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FVehicleType).HasColumnName("F车型").HasMaxLength(50);
        builder.Property(e => e.FSeatCount).HasColumnName("F座位数");
        builder.Property(e => e.FDriverName).HasColumnName("F司机姓名").HasMaxLength(50);
        builder.Property(e => e.FDriverPhone).HasColumnName("F司机电话").HasMaxLength(20);
        builder.Property(e => e.FSource).HasColumnName("F来源").HasMaxLength(20);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(200);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FEventId).HasDatabaseName("IX_CONF车辆_活动ID");
        builder.HasIndex(e => e.FPlateNumber).HasDatabaseName("IX_CONF车辆_车牌号");
    }
}
