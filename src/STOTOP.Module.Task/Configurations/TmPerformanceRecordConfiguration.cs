using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmPerformanceRecordConfiguration : IEntityTypeConfiguration<TmPerformanceRecord>
{
    public void Configure(EntityTypeBuilder<TmPerformanceRecord> builder)
    {
        builder.ToTable("TM绩效考核记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FPeriodId).HasColumnName("F考核周期ID");
        builder.Property(e => e.FEmployeeId).HasColumnName("F被考核人ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTaskTotal).HasColumnName("F任务总数").HasDefaultValue(0);
        builder.Property(e => e.FCompletedCount).HasColumnName("F完成数").HasDefaultValue(0);
        builder.Property(e => e.FOnTimeCount).HasColumnName("F按时完成数").HasDefaultValue(0);
        builder.Property(e => e.FOverdueCount).HasColumnName("F逾期数").HasDefaultValue(0);
        builder.Property(e => e.FCompletionRate).HasColumnName("F完成率").HasColumnType("decimal(5,2)").HasDefaultValue(0m);
        builder.Property(e => e.FOnTimeRate).HasColumnName("F按时率").HasColumnType("decimal(5,2)").HasDefaultValue(0m);
        builder.Property(e => e.FGoalAchievementRate).HasColumnName("F目标达成率").HasColumnType("decimal(5,2)").HasDefaultValue(0m);
        builder.Property(e => e.FQualityScore).HasColumnName("F质量评分").HasColumnType("decimal(3,1)");
        builder.Property(e => e.FSelfScore).HasColumnName("F自评评分").HasColumnType("decimal(3,1)");
        builder.Property(e => e.FOverallScore).HasColumnName("F综合得分").HasColumnType("decimal(5,2)");
        builder.Property(e => e.FGrade).HasColumnName("F考核等级").HasMaxLength(10);
        builder.Property(e => e.FComment).HasColumnName("F评语");
        builder.Property(e => e.FSelfComment).HasColumnName("F自评");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FPeriodId, e.FEmployeeId })
            .IsUnique()
            .HasDatabaseName("UQ_TM绩效考核记录_周期_人员");
        builder.HasIndex(e => e.FEmployeeId).HasDatabaseName("IX_TM绩效考核记录_被考核人ID");

        builder.HasMany(e => e.Scores)
            .WithOne(e => e.Record)
            .HasForeignKey(e => e.FRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
