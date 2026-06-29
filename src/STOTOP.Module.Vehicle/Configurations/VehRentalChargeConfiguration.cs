using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Vehicle.Entities;

namespace STOTOP.Module.Vehicle.Configurations;

public class VehRentalChargeConfiguration : IEntityTypeConfiguration<VehRentalCharge>
{
    public void Configure(EntityTypeBuilder<VehRentalCharge> builder)
    {
        builder.ToTable("VEH租赁收费记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FVehicleId).HasColumnName("F车辆ID").IsRequired();
        builder.Property(e => e.FAssignmentId).HasColumnName("F分配ID").IsRequired();
        builder.Property(e => e.FEmployeeId).HasColumnName("F员工ID").IsRequired();
        builder.Property(e => e.FEmployeeName).HasColumnName("F员工姓名").HasMaxLength(100);
        builder.Property(e => e.FRentalStandardId).HasColumnName("F租赁标准ID");
        builder.Property(e => e.FChargePeriodStart).HasColumnName("F收费周期开始").IsRequired();
        builder.Property(e => e.FChargePeriodEnd).HasColumnName("F收费周期结束").IsRequired();
        builder.Property(e => e.FAmountDue).HasColumnName("F应收金额").HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.FAmountPaid).HasColumnName("F实收金额").HasPrecision(18, 2);
        builder.Property(e => e.FChargeStatus).HasColumnName("F收费状态").HasDefaultValue(1);
        builder.Property(e => e.FChargeDate).HasColumnName("F收费日期");
        builder.Property(e => e.FVoucherId).HasColumnName("F凭证ID");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(1000);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FVehicleId).HasDatabaseName("IX_VEH租赁收费记录_车辆ID");
        builder.HasIndex(e => e.FAssignmentId).HasDatabaseName("IX_VEH租赁收费记录_分配ID");
        builder.HasIndex(e => e.FEmployeeId).HasDatabaseName("IX_VEH租赁收费记录_员工ID");
        builder.HasIndex(e => e.FChargeStatus).HasDatabaseName("IX_VEH租赁收费记录_收费状态");
    }
}
