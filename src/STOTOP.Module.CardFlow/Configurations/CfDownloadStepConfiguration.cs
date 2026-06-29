using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfDownloadStepConfiguration : IEntityTypeConfiguration<CfDownloadStep>
{
    public void Configure(EntityTypeBuilder<CfDownloadStep> builder)
    {
        builder.ToTable("CF下载步骤");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID").IsRequired();
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").IsRequired();
        builder.Property(e => e.FActionType).HasColumnName("F操作类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FSelector).HasColumnName("F选择器").HasMaxLength(500);
        builder.Property(e => e.FValue).HasColumnName("F值").HasMaxLength(500);
        builder.Property(e => e.FWaitTime).HasColumnName("F等待时间");
        builder.Property(e => e.FDescription).HasColumnName("F说明").HasMaxLength(200);

        builder.HasIndex(e => new { e.FTaskId, e.FSortOrder }).HasDatabaseName("IX_CF下载步骤_任务排序");
    }
}
