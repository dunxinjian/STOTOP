using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Vehicle.Entities;

namespace STOTOP.Module.Vehicle.Configurations;

public class VehAssignmentConfiguration : IEntityTypeConfiguration<VehAssignment>
{
    public void Configure(EntityTypeBuilder<VehAssignment> builder)
    {
        builder.ToTable("VEH车辆分配");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FVehicleId).HasColumnName("F车辆ID").IsRequired();
        builder.Property(e => e.FEmployeeId).HasColumnName("F员工ID").IsRequired();
        builder.Property(e => e.FEmployeeName).HasColumnName("F员工姓名").HasMaxLength(100);
        builder.Property(e => e.FAssignmentType).HasColumnName("F分配类型").HasDefaultValue(1);
        builder.Property(e => e.FStartDate).HasColumnName("F开始日期").IsRequired();
        builder.Property(e => e.FEndDate).HasColumnName("F结束日期");
        builder.Property(e => e.FAssignmentStatus).HasColumnName("F分配状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FVehicleId).HasDatabaseName("IX_VEH车辆分配_车辆ID");
        builder.HasIndex(e => e.FEmployeeId).HasDatabaseName("IX_VEH车辆分配_员工ID");

        builder.HasMany(e => e.RentalCharges)
            .WithOne(e => e.Assignment)
            .HasForeignKey(e => e.FAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
