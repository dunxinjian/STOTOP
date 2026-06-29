using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfBatchSnapshotConfiguration : IEntityTypeConfiguration<CfBatchSnapshot>
{
    public void Configure(EntityTypeBuilder<CfBatchSnapshot> builder)
    {
        builder.ToTable("CF批次快照");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID").IsRequired();
        builder.Property(e => e.FAutoPluginIndex).HasColumnName("FAutoPlugin序号").IsRequired();
        builder.Property(e => e.FAutoPluginName).HasColumnName("FAutoPlugin名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FSnapshotType).HasColumnName("F快照类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FStagingTable).HasColumnName("F暂存表名").HasMaxLength(200);
        builder.Property(e => e.FSnapshotData).HasColumnName("F快照数据").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FBatchId).HasDatabaseName("IX_CF批次快照_批次");
    }
}
