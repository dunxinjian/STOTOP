using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPriceSurchargeItemConfiguration : IEntityTypeConfiguration<ExpPriceSurchargeItem>
{
    public void Configure(EntityTypeBuilder<ExpPriceSurchargeItem> builder)
    {
        builder.ToTable("EXP快递报价_出港加收_配置项");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FSurchargeId).HasColumnName("F出港加收ID");
        builder.Property(e => e.FCalcMethod).HasColumnName("F计费方式");
        builder.Property(e => e.FWeightRoundingMethod).HasColumnName("F重量进位方式");
        builder.Property(e => e.FWeightFrom).HasColumnName("F起始重量").HasPrecision(10, 3);
        builder.Property(e => e.FWeightTo).HasColumnName("F截止重量").HasPrecision(10, 3);
        builder.Property(e => e.FWeightType).HasColumnName("F重量类型").HasDefaultValue(1);
        builder.Property(e => e.FDailyVolumeFrom).HasColumnName("F日单量起");
        builder.Property(e => e.FDailyVolumeTo).HasColumnName("F日单量止");
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasPrecision(10, 4);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").HasDefaultValue(0);

        builder.HasMany(e => e.Destinations)
            .WithOne()
            .HasForeignKey(d => d.FSurchargeItemId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
