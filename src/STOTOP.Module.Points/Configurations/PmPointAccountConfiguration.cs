using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Configurations;

public class PmPointAccountConfiguration : IEntityTypeConfiguration<PmPointAccount>
{
    public void Configure(EntityTypeBuilder<PmPointAccount> builder)
    {
        builder.ToTable("PM积分账户");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FTotalPoints).HasColumnName("F总积分").HasDefaultValue(0);
        builder.Property(e => e.FUsedPoints).HasColumnName("F已用积分").HasDefaultValue(0);
        builder.Property(e => e.FAvailablePoints).HasColumnName("F可用积分").HasDefaultValue(0);
        builder.Property(e => e.FMonthlyAward).HasColumnName("F本月奖分").HasDefaultValue(0);
        builder.Property(e => e.FMonthlyDeduct).HasColumnName("F本月扣分").HasDefaultValue(0);
        builder.Property(e => e.FYearlyPoints).HasColumnName("F本年积分").HasDefaultValue(0);
        builder.Property(e => e.F账户类型).HasColumnName("F账户类型").HasDefaultValue(1);
        builder.Property(e => e.F期初余额快照日期).HasColumnName("F期初余额快照日期");
        builder.Property(e => e.F期初余额快照值).HasColumnName("F期初余额快照值").HasDefaultValue(0);
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.FUserId, e.F账户类型 }).IsUnique().HasDatabaseName("UQ_PM积分账户_组织_用户_账户类型");
    }
}
