using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpClientWeightCapConfiguration : IEntityTypeConfiguration<ExpClientWeightCap>
{
    public void Configure(EntityTypeBuilder<ExpClientWeightCap> builder)
    {
        builder.ToTable("EXP均重上限");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("NCHAR(2)");
        builder.Property(e => e.FMaxAvgWeight).HasColumnName("F最大均重").HasPrecision(10, 3);
        builder.Property(e => e.FExcessUnit).HasColumnName("F超出单位").HasPrecision(10, 3);
        builder.Property(e => e.FExcessUnitPrice).HasColumnName("F超出单价").HasPrecision(10, 4);
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期");
        builder.Property(e => e.FExpiryDate).HasColumnName("F失效日期");
        builder.Property(e => e.FEnabled).HasColumnName("F启用").HasDefaultValue(true);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_EXP均重上限_租户ID");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(200);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FBusinessObjectId, e.FBrandCode, e.FEnabled })
            .HasDatabaseName("IX_EXP均重上限_业务对象品牌启用");
    }
}
