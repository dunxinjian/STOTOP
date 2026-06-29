using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.KSF.Entities;

namespace STOTOP.Module.KSF.Configurations;

public class KsfPlanDetailConfiguration : IEntityTypeConfiguration<KsfPlanDetail>
{
    public void Configure(EntityTypeBuilder<KsfPlanDetail> builder)
    {
        builder.ToTable("KSF岗位方案明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F方案ID).HasColumnName("F方案ID");
        builder.Property(e => e.F指标ID).HasColumnName("F指标ID");
        builder.Property(e => e.F权重).HasColumnName("F权重").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F平衡点).HasColumnName("F平衡点").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F激励刻度JSON).HasColumnName("F激励刻度JSON");
        builder.Property(e => e.F最低保底).HasColumnName("F最低保底").HasColumnType("decimal(18,4)").HasDefaultValue(0m);

        builder.HasIndex(e => new { e.F方案ID, e.F指标ID }).IsUnique().HasDatabaseName("UQ_KSF方案明细_方案_指标");
    }
}
