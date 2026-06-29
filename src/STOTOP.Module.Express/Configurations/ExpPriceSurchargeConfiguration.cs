using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpPriceSurchargeConfiguration : IEntityTypeConfiguration<ExpPriceSurcharge>
{
    public void Configure(EntityTypeBuilder<ExpPriceSurcharge> builder)
    {
        builder.ToTable("EXP快递报价_出港加收");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)").IsRequired().HasDefaultValue("");
        builder.Property(e => e.FSourceId).HasColumnName("F源FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FSurchargeType).HasColumnName("F类型");
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期");
        builder.Property(e => e.FIsActive).HasColumnName("F启用").HasDefaultValue(true);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasMany(e => e.Items)
            .WithOne(i => i.Surcharge)
            .HasForeignKey(i => i.FSurchargeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(e => e.FScope).HasColumnName("F作用域").HasDefaultValue(0);
        builder.Property(e => e.FNetworkPointCode).HasColumnName("F网点编码").HasMaxLength(20);
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50);
        builder.Property(e => e.FExpiryDate).HasColumnName("F失效日期");
    }
}
