using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysSecurityConfigConfiguration : IEntityTypeConfiguration<SysSecurityConfig>
{
    public void Configure(EntityTypeBuilder<SysSecurityConfig> builder)
    {
        builder.ToTable("SYS安全配置");

        builder.HasKey(e => e.FID);
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FConfigKey).HasColumnName("FConfigKey").HasMaxLength(100);
        builder.Property(e => e.FConfigValue).HasColumnName("FConfigValue").HasMaxLength(500);
        builder.Property(e => e.FDescription).HasColumnName("FDescription").HasMaxLength(200);
        builder.Property(e => e.FUpdateTime).HasColumnName("FUpdateTime").HasDefaultValueSql("getdate()");
        builder.Property(e => e.FUpdatedBy).HasColumnName("FUpdatedBy").HasMaxLength(50);

        builder.HasIndex(e => e.FConfigKey).IsUnique().HasDatabaseName("UQ_SYS安全配置_FConfigKey");
    }
}
