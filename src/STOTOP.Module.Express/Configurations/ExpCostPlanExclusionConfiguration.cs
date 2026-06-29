using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpCostPlanExclusionConfiguration : IEntityTypeConfiguration<ExpCostPlanExclusion>
{
    public void Configure(EntityTypeBuilder<ExpCostPlanExclusion> builder)
    {
        builder.ToTable("EXP成本方案_互斥配置");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FPlanId).HasColumnName("F方案ID");
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期");
        builder.Property(e => e.FExclusionRuleJson).HasColumnName("F互斥规则JSON");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FPlanId, e.FEffectiveDate })
            .HasDatabaseName("IX_EXP成本方案_互斥配置_方案生效日期");
    }
}
