using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpQuotationSurchargeLinkConfiguration : IEntityTypeConfiguration<ExpQuotationSurchargeLink>
{
    public void Configure(EntityTypeBuilder<ExpQuotationSurchargeLink> builder)
    {
        builder.ToTable("EXP快递报价_加收关联");

        builder.HasKey(e => new { e.F报价ID, e.F出港加收ID });

        builder.Property(e => e.F报价ID).HasColumnName("F报价ID");
        builder.Property(e => e.F出港加收ID).HasColumnName("F出港加收ID");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("getdate()");

        builder.HasIndex(e => e.F出港加收ID).HasDatabaseName("IX_EXP快递报价_加收关联_加收ID");
    }
}
