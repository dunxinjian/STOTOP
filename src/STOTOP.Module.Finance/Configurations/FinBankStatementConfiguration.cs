using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinBankStatementConfiguration : IEntityTypeConfiguration<FinBankStatement>
{
    public void Configure(EntityTypeBuilder<FinBankStatement> builder)
    {
        builder.ToTable("FIN银行流水");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("FAccountSetId").HasDefaultValue(0L);
        builder.Property(e => e.FBankAccount).HasColumnName("FBankAccount").HasMaxLength(50);
        builder.Property(e => e.FBankName).HasColumnName("FBankName").HasMaxLength(100);
        builder.Property(e => e.FTransactionDate).HasColumnName("FTransactionDate");
        builder.Property(e => e.FDescription).HasColumnName("FDescription").HasMaxLength(500);
        builder.Property(e => e.FDebitAmount).HasColumnName("FDebitAmount").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FCreditAmount).HasColumnName("FCreditAmount").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FBalance).HasColumnName("FBalance").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FCounterparty).HasColumnName("FCounterparty").HasMaxLength(200);
        builder.Property(e => e.FReferenceNo).HasColumnName("FReferenceNo").HasMaxLength(100);
        builder.Property(e => e.FMatchStatus).HasColumnName("FMatchStatus").HasDefaultValue(0);
        builder.Property(e => e.FMatchedVoucherId).HasColumnName("FMatchedVoucherId");
        builder.Property(e => e.FImportBatchId).HasColumnName("FImportBatchId");
        builder.Property(e => e.FCreatedTime).HasColumnName("FCreatedTime");
        builder.Property(e => e.FUpdatedTime).HasColumnName("FUpdatedTime");

        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN银行流水_AccountSetId");
        builder.HasIndex(e => e.FTransactionDate).HasDatabaseName("IX_FIN银行流水_TransactionDate");
        builder.HasIndex(e => e.FMatchStatus).HasDatabaseName("IX_FIN银行流水_MatchStatus");
    }
}
