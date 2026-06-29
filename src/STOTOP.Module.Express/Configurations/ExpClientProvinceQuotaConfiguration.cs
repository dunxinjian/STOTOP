using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpClientProvinceQuotaConfiguration : IEntityTypeConfiguration<ExpClientProvinceQuota>
{
    public void Configure(EntityTypeBuilder<ExpClientProvinceQuota> builder)
    {
        builder.ToTable("EXP目的地占比");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FProvinceId).HasColumnName("F省份ID");
        builder.Property(e => e.FMaxRatio).HasColumnName("F最大占比").HasPrecision(5, 2);
        builder.Property(e => e.FExcessCalcMethod).HasColumnName("F超出计费方式").HasDefaultValue(1);
        builder.Property(e => e.FExcessAmount).HasColumnName("F超出金额").HasPrecision(10, 4);
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期");
        builder.Property(e => e.FExpiryDate).HasColumnName("F失效日期");
        builder.Property(e => e.FEnabled).HasColumnName("F启用").HasDefaultValue(true);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(200);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FBusinessObjectId, e.FBrandCode, e.FProvinceId, e.FEnabled })
            .HasDatabaseName("IX_EXP目的地占比_业务对象品牌省份启用");
    }
}
