using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmPerformanceScoreConfiguration : IEntityTypeConfiguration<TmPerformanceScore>
{
    public void Configure(EntityTypeBuilder<TmPerformanceScore> builder)
    {
        builder.ToTable("TM绩效维度评分");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRecordId).HasColumnName("F考核记录ID");
        builder.Property(e => e.FDimensionId).HasColumnName("F维度ID");
        builder.Property(e => e.FScore).HasColumnName("F得分").HasColumnType("decimal(5,2)");
        builder.Property(e => e.FEvaluator).HasColumnName("F评价人").HasMaxLength(10);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);

        builder.HasIndex(e => new { e.FRecordId, e.FDimensionId, e.FEvaluator })
            .IsUnique()
            .HasDatabaseName("UQ_TM绩效维度评分_记录_维度_评价人");
    }
}
