using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysSecurityAuditLogConfiguration : IEntityTypeConfiguration<SysSecurityAuditLog>
{
    public void Configure(EntityTypeBuilder<SysSecurityAuditLog> builder)
    {
        builder.ToTable("SYS安全审计日志");

        builder.HasKey(e => e.FID);
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUserId).HasColumnName("FUserId");
        builder.Property(e => e.FAccount).HasColumnName("FAccount").HasMaxLength(50);
        builder.Property(e => e.FEventType).HasColumnName("FEventType").HasMaxLength(30);
        builder.Property(e => e.FEventResult).HasColumnName("FEventResult").HasMaxLength(10);
        builder.Property(e => e.FIpAddress).HasColumnName("FIpAddress").HasMaxLength(50);
        builder.Property(e => e.FDeviceFingerprint).HasColumnName("FDeviceFingerprint").HasMaxLength(256);
        builder.Property(e => e.FDeviceInfo).HasColumnName("FDeviceInfo").HasMaxLength(500);
        builder.Property(e => e.FFailReason).HasColumnName("FFailReason").HasMaxLength(200);
        builder.Property(e => e.FSessionId).HasColumnName("FSessionId").HasMaxLength(128);
        builder.Property(e => e.FExtraData).HasColumnName("FExtraData");
        builder.Property(e => e.FCreateTime).HasColumnName("FCreateTime").HasDefaultValueSql("getdate()");

        builder.HasIndex(e => e.FCreateTime).HasDatabaseName("IX_SYS安全审计日志_FCreateTime");
    }
}
