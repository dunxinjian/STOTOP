using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Configurations;

public class PmManagerQuotaConfiguration : IEntityTypeConfiguration<PmManagerQuota>
{
    public void Configure(EntityTypeBuilder<PmManagerQuota> builder)
    {
        builder.ToTable("PM管理层奖扣任务");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FManagerId).HasColumnName("F管理者ID");
        builder.Property(e => e.FYearMonth).HasColumnName("F年月").HasMaxLength(7).IsRequired();
        builder.Property(e => e.FAwardQuota).HasColumnName("F奖分配额");
        builder.Property(e => e.FDeductQuota).HasColumnName("F扣分配额");
        builder.Property(e => e.FUsedAward).HasColumnName("F已用奖分").HasDefaultValue(0);
        builder.Property(e => e.FUsedDeduct).HasColumnName("F已用扣分").HasDefaultValue(0);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.FManagerId, e.FYearMonth }).IsUnique().HasDatabaseName("UQ_PM管理层奖扣任务_组织_管理者_年月");
    }
}
