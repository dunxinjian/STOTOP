using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmPerformanceDimensionConfiguration : IEntityTypeConfiguration<TmPerformanceDimension>
{
    public void Configure(EntityTypeBuilder<TmPerformanceDimension> builder)
    {
        builder.ToTable("TM评价维度配置");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FDimensionName).HasColumnName("F维度名称").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FDimensionCode).HasColumnName("F维度编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FDataSource).HasColumnName("F数据来源");
        builder.Property(e => e.FWeight).HasColumnName("F权重").HasDefaultValue(100);
        builder.Property(e => e.FMaxScore).HasColumnName("F满分").HasColumnType("decimal(5,2)").HasDefaultValue(100m);
        builder.Property(e => e.FSort).HasColumnName("F排序").HasDefaultValue(0);
        builder.Property(e => e.FIsEnabled).HasColumnName("F是否启用").HasDefaultValue(true);

        builder.HasIndex(e => new { e.FOrgId, e.FDimensionCode })
            .IsUnique()
            .HasDatabaseName("UQ_TM评价维度_组织_编码");

        builder.HasMany(e => e.Scores)
            .WithOne(e => e.Dimension)
            .HasForeignKey(e => e.FDimensionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
