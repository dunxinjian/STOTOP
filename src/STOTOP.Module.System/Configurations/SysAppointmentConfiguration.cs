using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysAppointmentConfiguration : IEntityTypeConfiguration<SysAppointment>
{
    public void Configure(EntityTypeBuilder<SysAppointment> builder)
    {
        builder.ToTable("SYS任职");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.Property(e => e.FMemberId).HasColumnName("F成员ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FDirectSuperiorId).HasColumnName("F直属上级ID");
        builder.Property(e => e.FIsPrimary).HasColumnName("F是否主任职").HasDefaultValue(false);
        builder.Property(e => e.FScopeEligible).HasColumnName("F可参与范围放大").HasDefaultValue(false);
        builder.Property(e => e.FPosition).HasColumnName("F岗位").HasMaxLength(100);
        builder.Property(e => e.FJobNumber).HasColumnName("F工号").HasMaxLength(50);
        builder.Property(e => e.FEntryDate).HasColumnName("F入职日期");
        builder.Property(e => e.FIsCurrent).HasColumnName("F是否在职").HasDefaultValue(true);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FMemberId).HasDatabaseName("IX_SYS任职_成员ID");
        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_SYS任职_组织ID");
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_SYS任职_租户ID");
    }
}
