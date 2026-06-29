using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpQuotationChangeLogConfiguration : IEntityTypeConfiguration<ExpQuotationChangeLog>
{
    public void Configure(EntityTypeBuilder<ExpQuotationChangeLog> builder)
    {
        builder.ToTable("EXP快递报价_变更日志");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FQuotationId).HasColumnName("F报价ID");
        builder.Property(e => e.FChangeTime).HasColumnName("F变更时间");
        builder.Property(e => e.FChangedBy).HasColumnName("F变更人").HasMaxLength(50);
        builder.Property(e => e.FChangeType).HasColumnName("F变更类型");
        builder.Property(e => e.FBeforeContent).HasColumnName("F变更前内容");
        builder.Property(e => e.FAfterContent).HasColumnName("F变更后内容");
        builder.Property(e => e.FChangeReason).HasColumnName("F变更原因").HasMaxLength(500);

        builder.HasIndex(e => e.FQuotationId).HasDatabaseName("IX_EXP快递报价_变更日志_报价ID");
    }
}
