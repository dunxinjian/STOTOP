using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfDownloadLogConfiguration : IEntityTypeConfiguration<CfDownloadLog>
{
    public void Configure(EntityTypeBuilder<CfDownloadLog> builder)
    {
        builder.ToTable("CF下载日志");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID").IsRequired();
        builder.Property(e => e.FStartTime).HasColumnName("F开始时间").IsRequired();
        builder.Property(e => e.FEndTime).HasColumnName("F结束时间");
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FDownloadFileCount).HasColumnName("F下载文件数");
        builder.Property(e => e.FFilePathList).HasColumnName("F文件路径列表");
        builder.Property(e => e.FErrorMessage).HasColumnName("F错误信息");

        builder.HasIndex(e => e.FTaskId).HasDatabaseName("IX_CF下载日志_任务");
    }
}
