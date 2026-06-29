using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpCostPlanItemConfiguration : IEntityTypeConfiguration<ExpCostPlanItem>
{
    public void Configure(EntityTypeBuilder<ExpCostPlanItem> builder)
    {
        builder.ToTable("EXP成本方案_成本项");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FPlanId).HasColumnName("F方案ID");
        builder.Property(e => e.FItemName).HasColumnName("F成本项名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FItemType).HasColumnName("F成本项类型").HasDefaultValue(1);
        builder.Property(e => e.FSettlementWeightStage).HasColumnName("F结算重量环节");
        builder.Property(e => e.FSortOrder).HasColumnName("F排序号").HasDefaultValue(0);

        builder.HasMany(e => e.Outlets)
            .WithOne(o => o.Item)
            .HasForeignKey(o => o.FItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Shops)
            .WithOne(s => s.Item)
            .HasForeignKey(s => s.FItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Periods)
            .WithOne(p => p.Item)
            .HasForeignKey(p => p.FItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
