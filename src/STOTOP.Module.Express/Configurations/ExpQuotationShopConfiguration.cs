using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpQuotationShopConfiguration : IEntityTypeConfiguration<ExpQuotationShop>
{
    public void Configure(EntityTypeBuilder<ExpQuotationShop> builder)
    {
        builder.ToTable("EXP快递报价_关联店铺");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FQuotationId).HasColumnName("F报价ID");
        builder.Property(e => e.FShopName).HasColumnName("F店铺名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FQuotationId, e.FShopName }).IsUnique()
            .HasDatabaseName("IX_EXP快递报价_关联店铺_报价店铺");

        builder.HasOne(e => e.Quotation)
            .WithMany(q => q.Shops)
            .HasForeignKey(e => e.FQuotationId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
