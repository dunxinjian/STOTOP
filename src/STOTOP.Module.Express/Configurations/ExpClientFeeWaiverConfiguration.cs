using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpClientFeeWaiverConfiguration : IEntityTypeConfiguration<ExpClientFeeWaiver>
{
    public void Configure(EntityTypeBuilder<ExpClientFeeWaiver> builder)
    {
        builder.ToTable("EXP费用减免");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FWaiverType).HasColumnName("F减免类型");
        builder.Property(e => e.FWaiverName).HasColumnName("F减免名称").HasMaxLength(50);
        builder.Property(e => e.FIsActive).HasColumnName("F启用").HasDefaultValue(true);
        builder.Property(e => e.FEffectiveDate).HasColumnName("F生效日期");
        builder.Property(e => e.FExpiryDate).HasColumnName("F失效日期");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_EXP费用减免_租户ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FBusinessObjectId, e.FIsActive }).HasDatabaseName("IX_EXP费用减免_业务对象启用");
    }
}
