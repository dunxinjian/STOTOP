using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfCardBalanceConfiguration : IEntityTypeConfiguration<CfCardBalance>
{
    public void Configure(EntityTypeBuilder<CfCardBalance> builder)
    {
        builder.ToTable("CF卡片余额");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCardId).HasColumnName("F卡片ID");
        builder.Property(e => e.FOriginalAmount).HasColumnName("F原始金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FOffsetAmount).HasColumnName("F已冲销金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FRemainingAmount).HasColumnName("F剩余金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => e.FCardId).HasDatabaseName("IX_CF卡片余额_卡片");
    }
}
