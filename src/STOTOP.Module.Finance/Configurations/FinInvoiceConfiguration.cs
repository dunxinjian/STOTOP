using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinInvoiceConfiguration : IEntityTypeConfiguration<FinInvoice>
{
    public void Configure(EntityTypeBuilder<FinInvoice> builder)
    {
        builder.ToTable("FIN发票");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("FAccountSetId").HasDefaultValue(0L);
        builder.Property(e => e.FInvoiceType).HasColumnName("FInvoiceType").HasMaxLength(50);
        builder.Property(e => e.FInvoiceNo).HasColumnName("FInvoiceNo").HasMaxLength(50);
        builder.Property(e => e.FInvoiceCode).HasColumnName("FInvoiceCode").HasMaxLength(50);
        builder.Property(e => e.FInvoiceDate).HasColumnName("FInvoiceDate");
        builder.Property(e => e.FSellerName).HasColumnName("FSellerName").HasMaxLength(200);
        builder.Property(e => e.FSellerTaxNo).HasColumnName("FSellerTaxNo").HasMaxLength(50);
        builder.Property(e => e.FBuyerName).HasColumnName("FBuyerName").HasMaxLength(200);
        builder.Property(e => e.FBuyerTaxNo).HasColumnName("FBuyerTaxNo").HasMaxLength(50);
        builder.Property(e => e.FAmount).HasColumnName("FAmount").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FTaxAmount).HasColumnName("FTaxAmount").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FTotalAmount).HasColumnName("FTotalAmount").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FTaxRate).HasColumnName("FTaxRate").HasColumnType("DECIMAL(5,2)");
        builder.Property(e => e.FDirection).HasColumnName("FDirection").HasMaxLength(10);
        builder.Property(e => e.FMatchStatus).HasColumnName("FMatchStatus").HasDefaultValue(0);
        builder.Property(e => e.FMatchedVoucherId).HasColumnName("FMatchedVoucherId");
        builder.Property(e => e.FImportBatchId).HasColumnName("FImportBatchId");
        builder.Property(e => e.FStatus).HasColumnName("FStatus").HasDefaultValue(1);
        builder.Property(e => e.FCreatedTime).HasColumnName("FCreatedTime");
        builder.Property(e => e.FUpdatedTime).HasColumnName("FUpdatedTime");

        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN发票_AccountSetId");
        builder.HasIndex(e => e.FInvoiceNo).HasDatabaseName("IX_FIN发票_InvoiceNo");
        builder.HasIndex(e => e.FInvoiceDate).HasDatabaseName("IX_FIN发票_InvoiceDate");
        builder.HasIndex(e => e.FMatchStatus).HasDatabaseName("IX_FIN发票_MatchStatus");
    }
}
