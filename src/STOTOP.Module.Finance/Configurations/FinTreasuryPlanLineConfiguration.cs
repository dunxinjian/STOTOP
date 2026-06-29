using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinTreasuryPlanLineConfiguration : IEntityTypeConfiguration<FinTreasuryPlanLine>
{
    public void Configure(EntityTypeBuilder<FinTreasuryPlanLine> builder)
    {
        builder.ToTable("FIN资金计划明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FPlanDate).HasColumnName("F计划日期");
        builder.Property(e => e.FWeekStartDate).HasColumnName("F周开始日期");
        builder.Property(e => e.FDirection).HasColumnName("F方向").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FCashCategory).HasColumnName("F资金分类").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.FProbability).HasColumnName("F概率").HasColumnType("decimal(5,2)").HasDefaultValue(100m);
        builder.Property(e => e.FSourceType).HasColumnName("F来源类型").HasMaxLength(50).HasDefaultValue("manual");
        builder.Property(e => e.FSourceId).HasColumnName("F来源ID");
        builder.Property(e => e.FCounterpartyName).HasColumnName("F往来方名称").HasMaxLength(200);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FAccountSetId, e.FWeekStartDate, e.FDirection, e.FCashCategory })
            .HasDatabaseName("IX_FIN资金计划明细_账套周方向分类");
        builder.HasIndex(e => new { e.FSourceType, e.FSourceId, e.FCashCategory })
            .HasDatabaseName("IX_FIN资金计划明细_来源分类");
    }
}
