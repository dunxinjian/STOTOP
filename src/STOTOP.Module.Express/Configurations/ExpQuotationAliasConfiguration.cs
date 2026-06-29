using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpQuotationAliasConfiguration : IEntityTypeConfiguration<ExpQuotationAlias>
{
    public void Configure(EntityTypeBuilder<ExpQuotationAlias> builder)
    {
        builder.ToTable("EXP快递报价_共享别名");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FQuotationId).HasColumnName("F报价ID");
        builder.Property(e => e.FAlias).HasColumnName("F别名").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => new { e.FQuotationId, e.FAlias }).IsUnique()
            .HasDatabaseName("IX_EXP快递报价_共享别名_报价别名");

        builder.HasOne(e => e.Quotation)
            .WithMany(q => q.Aliases)
            .HasForeignKey(e => e.FQuotationId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
