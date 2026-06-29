using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysConfigConfiguration : IEntityTypeConfiguration<SysConfig>
{
    public void Configure(EntityTypeBuilder<SysConfig> builder)
    {
        builder.ToTable("SYS系统参数");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FKey).HasColumnName("F参数键").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FValue).HasColumnName("F参数值").IsRequired();
        builder.Property(e => e.FDataType).HasColumnName("F参数类型").HasMaxLength(20).IsRequired().HasDefaultValue("string");
        builder.Property(e => e.FDescription).HasColumnName("F参数说明").HasMaxLength(500);
        builder.Property(e => e.FIsBuiltIn).HasColumnName("F是否内置").HasDefaultValue(false);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FKey).IsUnique();
    }
}
