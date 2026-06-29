using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Configurations;

public class PmPointRankingConfiguration : IEntityTypeConfiguration<PmPointRanking>
{
    public void Configure(EntityTypeBuilder<PmPointRanking> builder)
    {
        builder.ToTable("PM积分排名快照");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FDepartmentId).HasColumnName("F部门ID");
        builder.Property(e => e.FDimension).HasColumnName("F维度");
        builder.Property(e => e.FPeriod).HasColumnName("F周期").HasMaxLength(10).IsRequired();
        builder.Property(e => e.FTotalPoints).HasColumnName("F总积分");
        builder.Property(e => e.FAwardPoints).HasColumnName("F奖分");
        builder.Property(e => e.FDeductPoints).HasColumnName("F扣分");
        builder.Property(e => e.FRank).HasColumnName("F排名");
        builder.Property(e => e.F是否主组织).HasColumnName("F是否主组织").HasDefaultValue(true);
        builder.Property(e => e.F经营单元ID).HasColumnName("F经营单元ID");
        builder.Property(e => e.FGenerateTime).HasColumnName("F生成时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.FDimension, e.FPeriod }).HasDatabaseName("IX_PM积分排名_组织_维度_周期");
        builder.HasIndex(e => e.FDepartmentId).HasDatabaseName("IX_PM积分排名_部门");
    }
}
