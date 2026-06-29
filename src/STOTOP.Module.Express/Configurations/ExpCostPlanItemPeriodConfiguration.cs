using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpCostPlanItemPeriodConfiguration : IEntityTypeConfiguration<ExpCostPlanItemPeriod>
{
    public void Configure(EntityTypeBuilder<ExpCostPlanItemPeriod> builder)
    {
        builder.ToTable("EXP成本方案_成本项_时间段");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FItemId).HasColumnName("F成本项ID");
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期");
        builder.Property(e => e.FMatrixJson).HasColumnName("F矩阵JSON");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FItemId, e.FEffectiveDate })
            .IsUnique()
            .HasDatabaseName("IX_EXP成本方案_成本项_时间段_项生效日期");
    }
}
