using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinVoucherEntryConfiguration : IEntityTypeConfiguration<FinVoucherEntry>
{
    public void Configure(EntityTypeBuilder<FinVoucherEntry> builder)
    {
        builder.ToTable("FIN凭证分录");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FVoucherId).HasColumnName("F凭证ID");
        builder.Property(e => e.FLineNo).HasColumnName("F行号");
        builder.Property(e => e.FSummary).HasColumnName("F摘要").HasMaxLength(500);
        builder.Property(e => e.FAccountId).HasColumnName("F科目ID");
        builder.Property(e => e.FAccountCode).HasColumnName("F科目编码").HasMaxLength(50);
        builder.Property(e => e.FAccountName).HasColumnName("F科目名称").HasMaxLength(200);
        builder.Property(e => e.FAuxiliaryJson).HasColumnName("F辅助核算JSON");
        builder.Property(e => e.FDebitAmount).HasColumnName("F借方金额").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FCreditAmount).HasColumnName("F贷方金额").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FCurrencyCode).HasColumnName("F币种代码").HasMaxLength(10);
        builder.Property(e => e.FExchangeRate).HasColumnName("F汇率").HasColumnType("DECIMAL(18,6)");
        builder.Property(e => e.FOriginalAmount).HasColumnName("F原币金额").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FDataScopeId).HasColumnName("F数据作用域ID").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        
        builder.HasIndex(e => e.FVoucherId).HasDatabaseName("IX_FIN凭证分录_凭证ID");
        builder.HasIndex(e => e.FAccountId).HasDatabaseName("IX_FIN凭证分录_科目ID");
        
        builder.HasOne<FinAccount>()
            .WithMany()
            .HasForeignKey(e => e.FAccountId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN凭证分录_科目ID");
    }
}
