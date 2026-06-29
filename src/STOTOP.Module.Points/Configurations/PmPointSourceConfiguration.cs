using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Configurations;

public class PmPointSourceConfiguration : IEntityTypeConfiguration<PmPointSource>
{
    public void Configure(EntityTypeBuilder<PmPointSource> builder)
    {
        builder.ToTable("PM积分来源");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FSourceName).HasColumnName("F来源名称").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FSourceCode).HasColumnName("F来源编码").HasMaxLength(30).IsRequired();
        builder.Property(e => e.FIcon).HasColumnName("F图标").HasMaxLength(50);
        builder.Property(e => e.FColor).HasColumnName("F颜色").HasMaxLength(20);
        builder.Property(e => e.FDescription).HasColumnName("F说明").HasMaxLength(200);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").HasDefaultValue(0);
        builder.Property(e => e.FIsEnabled).HasColumnName("F是否启用").HasDefaultValue(true);

        builder.HasIndex(e => new { e.FOrgId, e.FSourceCode }).IsUnique().HasDatabaseName("UQ_PM积分来源_组织_编码");
    }
}
