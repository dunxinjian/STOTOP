using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysThemeSettingConfiguration : IEntityTypeConfiguration<SysThemeSetting>
{
    public void Configure(EntityTypeBuilder<SysThemeSetting> builder)
    {
        builder.ToTable("SYS主题配置");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FConfigJson).HasColumnName("F配置JSON").IsRequired();
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateBy).HasColumnName("F更新人").HasMaxLength(100);
    }
}
