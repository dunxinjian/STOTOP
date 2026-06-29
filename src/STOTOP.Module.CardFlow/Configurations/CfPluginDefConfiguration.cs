using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfPluginDefConfiguration : IEntityTypeConfiguration<CfPluginDef>
{
    public void Configure(EntityTypeBuilder<CfPluginDef> builder)
    {
        builder.ToTable("CF自动插件");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F插件名称).HasColumnName("F插件名称").HasMaxLength(100);
        builder.Property(e => e.F插件类型).HasColumnName("F插件类型").HasMaxLength(50);
        builder.Property(e => e.F插件实现类型).HasColumnName("F插件实现类型").HasMaxLength(200);
        builder.Property(e => e.F排序号).HasColumnName("F排序号");
        builder.Property(e => e.F状态).HasColumnName("F状态");
        builder.Property(e => e.F输入来源类型).HasColumnName("F输入来源类型").HasMaxLength(50);
        builder.Property(e => e.F输入目标类型).HasColumnName("F输入目标类型").HasMaxLength(50);
        builder.Property(e => e.F目标表名).HasColumnName("F目标表名").HasMaxLength(200);
        builder.Property(e => e.F来源表名).HasColumnName("F来源表名").HasMaxLength(200);
        builder.Property(e => e.F配置JSON).HasColumnName("F配置JSON");
        builder.Property(e => e.F支持回撤).HasColumnName("F支持回撤");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间");
        builder.Property(e => e.F更新时间).HasColumnName("F更新时间");
        builder.Property(e => e.F规则ID).HasColumnName("F规则ID");

        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_CF自动插件_组织ID");

        builder.HasOne(e => e.Rule)
            .WithMany()
            .HasForeignKey(e => e.F规则ID)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
