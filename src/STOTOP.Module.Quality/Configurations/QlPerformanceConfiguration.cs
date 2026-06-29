using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlPerformanceConfiguration : IEntityTypeConfiguration<QlPerformance>
{
    public void Configure(EntityTypeBuilder<QlPerformance> builder)
    {
        builder.ToTable("QL绩效记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FPeriod).HasColumnName("F周期").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FExceptionCount).HasColumnName("F异常数");
        builder.Property(e => e.FResolvedCount).HasColumnName("F已解决数");
        builder.Property(e => e.FOverdueCount).HasColumnName("F超时数");
        builder.Property(e => e.FScore).HasColumnName("F评分").HasColumnType("decimal(5,2)");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.FUserId, e.FPeriod }).HasDatabaseName("IX_QL绩效记录_组织_用户_周期").IsUnique();
    }
}
