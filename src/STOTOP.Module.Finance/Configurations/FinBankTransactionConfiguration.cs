using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinBankTransactionConfiguration : IEntityTypeConfiguration<FinBankTransaction>
{
    public void Configure(EntityTypeBuilder<FinBankTransaction> builder)
    {
        builder.ToTable("FIN交易流水");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FChannelId).HasColumnName("F渠道ID");
        builder.Property(e => e.FTransactionDate).HasColumnName("F交易日期");
        builder.Property(e => e.FTransactionNo).HasColumnName("F交易流水号").HasMaxLength(100);
        builder.Property(e => e.FCounterpartAccount).HasColumnName("F对方账号").HasMaxLength(100);
        builder.Property(e => e.FCounterpartName).HasColumnName("F对方户名").HasMaxLength(200);
        builder.Property(e => e.FDirection).HasColumnName("F收支方向");
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasColumnType("DECIMAL(14,2)").HasDefaultValue(0m);
        builder.Property(e => e.FBalance).HasColumnName("F余额").HasColumnType("DECIMAL(14,2)");
        builder.Property(e => e.FSummary).HasColumnName("F摘要").HasMaxLength(500);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FImportBatchId).HasColumnName("F导入批次ID");
        builder.Property(e => e.FMatchStatus).HasColumnName("F匹配状态").HasDefaultValue(0);
        builder.Property(e => e.FRelatedBusinessType).HasColumnName("F关联业务类型").HasMaxLength(50);
        builder.Property(e => e.FRelatedBusinessId).HasColumnName("F关联业务ID");
        builder.Property(e => e.FVoucherId).HasColumnName("F凭证ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        // 索引
        builder.HasIndex(e => e.FTransactionNo)
            .IsUnique()
            .HasDatabaseName("UX_FIN银行流水_F交易流水号");
        builder.HasIndex(e => e.FChannelId).HasDatabaseName("IX_FIN银行流水_F渠道ID");
        builder.HasIndex(e => e.FTransactionDate).HasDatabaseName("IX_FIN银行流水_F交易日期");
        builder.HasIndex(e => e.FMatchStatus).HasDatabaseName("IX_FIN银行流水_F匹配状态");
        builder.HasIndex(e => e.FDirection).HasDatabaseName("IX_FIN银行流水_F收支方向");
        builder.HasIndex(e => e.FImportBatchId).HasDatabaseName("IX_FIN银行流水_F导入批次ID");
        builder.HasIndex(e => e.FVoucherId).HasDatabaseName("IX_FIN银行流水_F凭证ID");
        builder.HasIndex(e => new { e.FRelatedBusinessType, e.FRelatedBusinessId })
            .HasDatabaseName("IX_FIN银行流水_F关联业务");

        // 外键: FIN银行流水 -> FIN交易渠道
        builder.HasOne<FinPaymentChannel>()
            .WithMany()
            .HasForeignKey(e => e.FChannelId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN银行流水_渠道");

        // 外键: FIN银行流水 -> FIN凭证
        builder.HasOne<FinVoucher>()
            .WithMany()
            .HasForeignKey(e => e.FVoucherId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN银行流水_凭证");
    }
}
