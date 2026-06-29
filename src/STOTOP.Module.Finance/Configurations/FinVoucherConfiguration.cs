using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinVoucherConfiguration : IEntityTypeConfiguration<FinVoucher>
{
    public void Configure(EntityTypeBuilder<FinVoucher> builder)
    {
        builder.ToTable("FIN凭证");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FVoucherWord).HasColumnName("F凭证字").HasMaxLength(10);
        builder.Property(e => e.FVoucherNo).HasColumnName("F凭证号");
        builder.Property(e => e.FDate).HasColumnName("F日期");
        builder.Property(e => e.FPeriodId).HasColumnName("F期间ID");
        builder.Property(e => e.FAttachmentCount).HasColumnName("F附件数");
        builder.Property(e => e.FCreator).HasColumnName("F制单人").HasMaxLength(50);
        builder.Property(e => e.FAuditor).HasColumnName("F审核人").HasMaxLength(50);
        builder.Property(e => e.FModifier).HasColumnName("F修改人").HasMaxLength(50);
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FSource).HasColumnName("F来源").HasMaxLength(50);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID").HasDefaultValue(0L);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FDataScopeId).HasColumnName("F数据作用域ID").HasMaxLength(500);
        builder.Property(e => e.FIsRevoked).HasColumnName("F已撤销").HasDefaultValue(false);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        
        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN凭证_账套ID");
        builder.HasIndex(e => e.FDate).HasDatabaseName("IX_FIN凭证_日期");
        builder.HasIndex(e => e.FPeriodId).HasDatabaseName("IX_FIN凭证_期间ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_FIN凭证_状态");
        builder.HasIndex(e => new { e.FVoucherWord, e.FVoucherNo }).HasDatabaseName("IX_FIN凭证_凭证字号");
        
        builder.HasOne<FinAccountPeriod>()
            .WithMany()
            .HasForeignKey(e => e.FPeriodId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN凭证_期间ID");
        
        builder.HasMany(e => e.Entries)
            .WithOne(e => e.Voucher)
            .HasForeignKey(e => e.FVoucherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
