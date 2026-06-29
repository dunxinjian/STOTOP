using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfBatchRowConfiguration : IEntityTypeConfiguration<CfBatchRow>
{
    public void Configure(EntityTypeBuilder<CfBatchRow> builder)
    {
        builder.ToTable("CF批次明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID");
        builder.Property(e => e.FRowNo).HasColumnName("F行号");
        builder.Property(e => e.FDataJson).HasColumnName("F数据JSON");
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FErrorMessage).HasColumnName("F错误信息").HasMaxLength(2000);
        builder.Property(e => e.FCardId).HasColumnName("F关联卡片ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => e.FBatchId).HasDatabaseName("IX_CF批次明细_批次");
        builder.HasIndex(e => new { e.FBatchId, e.FStatus }).HasDatabaseName("IX_CF批次明细_批次状态");
        builder.HasIndex(e => e.FCardId).HasFilter("[F关联卡片ID] IS NOT NULL").HasDatabaseName("IX_CF批次明细_卡片");
    }
}
