using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysUserSessionConfiguration : IEntityTypeConfiguration<SysUserSession>
{
    public void Configure(EntityTypeBuilder<SysUserSession> builder)
    {
        builder.ToTable("SYS用户会话");

        builder.HasKey(e => e.FID);
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUserId).HasColumnName("FUserId");
        builder.Property(e => e.FSessionId).HasColumnName("FSessionId").HasMaxLength(128);
        builder.Property(e => e.FRefreshToken).HasColumnName("FRefreshToken").HasMaxLength(256);
        builder.Property(e => e.FRefreshTokenExpiry).HasColumnName("FRefreshTokenExpiry");
        builder.Property(e => e.FDeviceFingerprint).HasColumnName("FDeviceFingerprint").HasMaxLength(256);
        builder.Property(e => e.FDeviceInfo).HasColumnName("FDeviceInfo").HasMaxLength(500);
        builder.Property(e => e.FIpAddress).HasColumnName("FIpAddress").HasMaxLength(50);
        builder.Property(e => e.FLoginTime).HasColumnName("FLoginTime").HasDefaultValueSql("getdate()");
        builder.Property(e => e.FLastActiveTime).HasColumnName("FLastActiveTime").HasDefaultValueSql("getdate()");
        builder.Property(e => e.FStatus).HasColumnName("FStatus").HasDefaultValue(1);
        builder.Property(e => e.FLogoutTime).HasColumnName("FLogoutTime");
        builder.Property(e => e.FLogoutReason).HasColumnName("FLogoutReason").HasMaxLength(50);

        builder.HasIndex(e => e.FRefreshToken).HasDatabaseName("IX_SYS用户会话_FRefreshToken");
        builder.HasIndex(e => e.FSessionId).HasDatabaseName("IX_SYS用户会话_FSessionId");
        builder.HasIndex(e => e.FUserId).HasDatabaseName("IX_SYS用户会话_FUserId");
    }
}
