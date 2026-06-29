using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysDbConnectionConfiguration : IEntityTypeConfiguration<SysDbConnection>
{
    public void Configure(EntityTypeBuilder<SysDbConnection> builder)
    {
        builder.ToTable("SYS数据库连接");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FConnectionName).HasColumnName("F连接名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FDatabaseType).HasColumnName("F数据库类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FServer).HasColumnName("F服务器").HasMaxLength(200);
        builder.Property(e => e.FPort).HasColumnName("F端口");
        builder.Property(e => e.FDatabaseName).HasColumnName("F数据库名").HasMaxLength(200);
        builder.Property(e => e.FUsername).HasColumnName("F用户名").HasMaxLength(100);
        builder.Property(e => e.FPassword).HasColumnName("F密码").HasMaxLength(500);
        builder.Property(e => e.FFilePath).HasColumnName("F文件路径").HasMaxLength(500);
        builder.Property(e => e.FWindowsAuth).HasColumnName("FWindows认证").HasDefaultValue(0);
        builder.Property(e => e.FTrustServerCertificate).HasColumnName("F信任服务器证书").HasDefaultValue(0);
        builder.Property(e => e.FConnectionString).HasColumnName("F连接字符串").HasMaxLength(2000);
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.F说明).HasColumnName("F说明").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FConnectionName).IsUnique();
    }
}
