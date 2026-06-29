using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpCostPlanItemShopConfiguration : IEntityTypeConfiguration<ExpCostPlanItemShop>
{
    public void Configure(EntityTypeBuilder<ExpCostPlanItemShop> builder)
    {
        builder.ToTable("EXP成本方案_成本项_关联店铺");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FItemId).HasColumnName("F成本项ID");
        builder.Property(e => e.FShopName).HasColumnName("F店铺名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FItemId, e.FShopName })
            .IsUnique()
            .HasDatabaseName("IX_EXP成本方案_成本项_关联店铺_项店铺");
    }
}
