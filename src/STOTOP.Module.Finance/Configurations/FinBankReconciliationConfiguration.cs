using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinBankReconciliationConfiguration : IEntityTypeConfiguration<FinBankReconciliation>
{
    public void Configure(EntityTypeBuilder<FinBankReconciliation> builder)
    {
        builder.ToTable("FIN银行对账记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("FAccountSetId").HasDefaultValue(0L);
        builder.Property(e => e.FBankStatementId).HasColumnName("FBankStatementId");
        builder.Property(e => e.FVoucherId).HasColumnName("FVoucherId");
        builder.Property(e => e.FVoucherEntryId).HasColumnName("FVoucherEntryId");
        builder.Property(e => e.FMatchType).HasColumnName("FMatchType").HasMaxLength(20);
        builder.Property(e => e.FMatchTime).HasColumnName("FMatchTime");
        builder.Property(e => e.FOperatorId).HasColumnName("FOperatorId");

        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN银行对账记录_AccountSetId");
        builder.HasIndex(e => e.FBankStatementId).HasDatabaseName("IX_FIN银行对账记录_BankStatementId");
        builder.HasIndex(e => e.FVoucherId).HasDatabaseName("IX_FIN银行对账记录_VoucherId");

        builder.HasOne<FinBankStatement>()
            .WithMany()
            .HasForeignKey(e => e.FBankStatementId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN银行对账记录_BankStatementId");

        builder.HasOne<FinVoucher>()
            .WithMany()
            .HasForeignKey(e => e.FVoucherId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN银行对账记录_VoucherId");
    }
}
