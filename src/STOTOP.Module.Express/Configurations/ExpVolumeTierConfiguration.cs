using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpVolumeTierConfiguration : IEntityTypeConfiguration<ExpVolumeTier>
{
    public void Configure(EntityTypeBuilder<ExpVolumeTier> builder)
    {
        builder.ToTable("EXP发件量阶梯");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBusinessObjectId).HasColumnName("F业务对象ID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FMinMonthlyVolume).HasColumnName("F最低月发件量");
        builder.Property(e => e.FQuotationPlanId).HasColumnName("F报价方案ID");
        builder.Property(e => e.FIsActive).HasColumnName("F启用").HasDefaultValue(true);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0);
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID").HasDefaultValue(0L);
        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_EXP发件量阶梯_租户ID");
        builder.Property(e => e.FBrandCode).HasColumnName("F品牌编码").HasColumnType("nchar(2)");

        builder.HasIndex(e => new { e.FBusinessObjectId, e.FBrandCode, e.FMinMonthlyVolume })
            .HasDatabaseName("IX_EXP发件量阶梯_业务对象品牌发件量");
    }
}
