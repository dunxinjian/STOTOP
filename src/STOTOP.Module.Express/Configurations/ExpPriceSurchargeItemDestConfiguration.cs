using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPriceSurchargeItemDestConfiguration : IEntityTypeConfiguration<ExpPriceSurchargeItemDest>
{
    public void Configure(EntityTypeBuilder<ExpPriceSurchargeItemDest> builder)
    {
        builder.ToTable("EXP快递报价_出港加收_目的地");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FSurchargeItemId).HasColumnName("F配置项ID");
        builder.Property(e => e.FDestType).HasColumnName("F目的地类型").HasDefaultValue(1);
        builder.Property(e => e.FProvinceId).HasColumnName("F省份ID");
        builder.Property(e => e.FCityName).HasColumnName("F城市名").HasMaxLength(100);
    }
}
