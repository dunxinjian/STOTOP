using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAccountBalanceConfiguration : IEntityTypeConfiguration<FinAccountBalance>
{
    public void Configure(EntityTypeBuilder<FinAccountBalance> builder)
    {
        builder.ToTable("FIN科目余额");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FPeriodId).HasColumnName("F期间ID");
        builder.Property(e => e.FAccountId).HasColumnName("F科目ID");
        builder.Property(e => e.FBeginDebit).HasColumnName("F期初借方").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FBeginCredit).HasColumnName("F期初贷方").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FCurrentDebit).HasColumnName("F本期借方").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FCurrentCredit).HasColumnName("F本期贷方").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FEndDebit).HasColumnName("F期末借方").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FEndCredit).HasColumnName("F期末贷方").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID").HasDefaultValue(0L);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        
        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN科目余额_账套ID");
        builder.HasIndex(e => new { e.FPeriodId, e.FAccountId }).IsUnique().HasDatabaseName("IX_FIN科目余额_期间科目");
        builder.HasIndex(e => e.FAccountId).HasDatabaseName("IX_FIN科目余额_科目ID");
        
        builder.HasOne<FinAccountPeriod>()
            .WithMany()
            .HasForeignKey(e => e.FPeriodId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN科目余额_期间ID");
        
        builder.HasOne<FinAccount>()
            .WithMany()
            .HasForeignKey(e => e.FAccountId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN科目余额_科目ID");
    }
}
