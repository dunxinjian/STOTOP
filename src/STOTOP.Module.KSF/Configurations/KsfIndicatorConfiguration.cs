using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.KSF.Entities;

namespace STOTOP.Module.KSF.Configurations;

public class KsfIndicatorConfiguration : IEntityTypeConfiguration<KsfIndicator>
{
    public void Configure(EntityTypeBuilder<KsfIndicator> builder)
    {
        builder.ToTable("KSF指标定义");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F编码).HasColumnName("F编码").HasMaxLength(64).IsRequired();
        builder.Property(e => e.F名称).HasColumnName("F名称").HasMaxLength(128).IsRequired();
        builder.Property(e => e.F计量单位).HasColumnName("F计量单位").HasMaxLength(64);
        builder.Property(e => e.F取数类型).HasColumnName("F取数类型");
        builder.Property(e => e.F取数SQL).HasColumnName("F取数SQL");
        builder.Property(e => e.F取数Agent).HasColumnName("F取数Agent").HasMaxLength(128);
        builder.Property(e => e.F取数参数JSON).HasColumnName("F取数参数JSON");
        builder.Property(e => e.F方向).HasColumnName("F方向").HasDefaultValue(1);
        builder.Property(e => e.F业务对象类型).HasColumnName("F业务对象类型").HasMaxLength(16).IsRequired();
        builder.Property(e => e.F是否启用).HasColumnName("F是否启用").HasDefaultValue(true);
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.F更新时间).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F编码 }).IsUnique().HasDatabaseName("UQ_KSF指标_组织_编码");
    }
}
