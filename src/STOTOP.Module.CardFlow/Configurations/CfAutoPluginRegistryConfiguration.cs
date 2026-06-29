using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfAutoPluginRegistryConfiguration : IEntityTypeConfiguration<CfAutoPluginRegistry>
{
    public void Configure(EntityTypeBuilder<CfAutoPluginRegistry> builder)
    {
        builder.ToTable("CF自动插件注册");
        builder.HasKey(e => e.FID);

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F插件编码).HasColumnName("F插件编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F插件名称).HasColumnName("F插件名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.F插件类型).HasColumnName("F插件类型").HasMaxLength(20).IsRequired();
        builder.Property(e => e.F处理粒度).HasColumnName("F处理粒度").HasMaxLength(20).IsRequired();
        builder.Property(e => e.F默认配置JSON).HasColumnName("F默认配置JSON");
        builder.Property(e => e.F说明).HasColumnName("F说明").HasMaxLength(500);
        builder.Property(e => e.F状态).HasColumnName("F状态").IsRequired();

        builder.HasIndex(e => e.F插件编码).IsUnique().HasDatabaseName("UX_CF自动插件注册_F插件编码");
    }
}
