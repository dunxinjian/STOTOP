using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfVoucherRecordConfiguration : IEntityTypeConfiguration<CfVoucherRecord>
{
    public void Configure(EntityTypeBuilder<CfVoucherRecord> builder)
    {
        builder.ToTable("CF凭证记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID");
        builder.Property(e => e.FTargetTable).HasColumnName("F暂存表").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FTotalRows).HasColumnName("F总行数").HasDefaultValue(0);
        builder.Property(e => e.FMatchedRows).HasColumnName("F已匹配行数").HasDefaultValue(0);
        builder.Property(e => e.FUnmatchedRows).HasColumnName("F未匹配行数").HasDefaultValue(0);
        builder.Property(e => e.FUnmatchedDetailsJson).HasColumnName("F未匹配明细").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FGeneratedVoucherCount).HasColumnName("F生成凭证数").HasDefaultValue(0);
        builder.Property(e => e.FVoucherIdsJson).HasColumnName("F凭证ID列表").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FErrorMessage).HasColumnName("F错误信息").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FBatchId).HasDatabaseName("IX_CF凭证记录_批次");
    }
}
