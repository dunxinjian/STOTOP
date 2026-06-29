using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinTrialBalanceConfiguration : IEntityTypeConfiguration<FinTrialBalance>
{
    public void Configure(EntityTypeBuilder<FinTrialBalance> builder)
    {
        builder.ToTable("FIN试算平衡");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FPeriodId).HasColumnName("FPeriodId");
        builder.Property(e => e.FAccountSetId).HasColumnName("FAccountSetId").HasDefaultValue(0L);
        builder.Property(e => e.FTotalDebit).HasColumnName("FTotalDebit").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FTotalCredit).HasColumnName("FTotalCredit").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FIsBalanced).HasColumnName("FIsBalanced").HasDefaultValue(false);
        builder.Property(e => e.FDetails).HasColumnName("FDetails");
        builder.Property(e => e.FGeneratedTime).HasColumnName("FGeneratedTime");
        builder.Property(e => e.FOperatorId).HasColumnName("FOperatorId");

        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN试算平衡_AccountSetId");
        builder.HasIndex(e => e.FPeriodId).HasDatabaseName("IX_FIN试算平衡_PeriodId");

        builder.HasOne<FinAccountPeriod>()
            .WithMany()
            .HasForeignKey(e => e.FPeriodId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN试算平衡_PeriodId");
    }
}
