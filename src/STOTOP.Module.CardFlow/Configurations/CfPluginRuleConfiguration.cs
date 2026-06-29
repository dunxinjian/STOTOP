using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfPluginRuleConfiguration : IEntityTypeConfiguration<CfPluginRule>
{
    public void Configure(EntityTypeBuilder<CfPluginRule> builder)
    {
        builder.ToTable("CF自动插件_规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F类型编码).HasColumnName("F类型编码").HasMaxLength(50);
        builder.Property(e => e.F规则名称).HasColumnName("F规则名称").HasMaxLength(100);
        builder.Property(e => e.F规则配置JSON).HasColumnName("F规则配置JSON");
        builder.Property(e => e.F规则配置V1备份).HasColumnName("F规则配置V1备份");
        builder.Property(e => e.F状态).HasColumnName("F状态");
        builder.Property(e => e.F说明).HasColumnName("F说明").HasMaxLength(500);
        builder.Property(e => e.FConcurrencyStamp).HasColumnName("F并发戳").HasMaxLength(32);
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间");
        builder.Property(e => e.F更新时间).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_CF自动插件_规则_组织ID");
        builder.HasIndex(e => e.F类型编码).HasDatabaseName("IX_CF自动插件_规则_类型编码");
    }
}
