using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfBatchErrorConfiguration : IEntityTypeConfiguration<CfBatchError>
{
    public void Configure(EntityTypeBuilder<CfBatchError> builder)
    {
        builder.ToTable("CF批次错误");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID").IsRequired();
        builder.Property(e => e.FStagingId).HasColumnName("F暂存ID");
        builder.Property(e => e.FRowNumber).HasColumnName("F行号").IsRequired();
        builder.Property(e => e.FErrorType).HasColumnName("F错误类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FSeverityLevel).HasColumnName("F严重级别").HasMaxLength(20);
        builder.Property(e => e.FErrorField).HasColumnName("F错误字段").HasMaxLength(100);
        builder.Property(e => e.FErrorMessage).HasColumnName("F错误信息").HasMaxLength(500);
        builder.Property(e => e.FSuggestedFix).HasColumnName("F建议修复").HasMaxLength(500);
        builder.Property(e => e.FOriginalValue).HasColumnName("F原始值").HasMaxLength(200);
        builder.Property(e => e.FQualityDimension).HasColumnName("F质量维度").HasMaxLength(20);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.Property(e => e.FDispatchStatus).HasColumnName("F派发状态").HasMaxLength(20);
        builder.Property(e => e.FDispatchType).HasColumnName("F派发方式").HasMaxLength(20);
        builder.Property(e => e.FDispatchRecordId).HasColumnName("F派发记录ID");

        builder.Property(e => e.FWorkItemId).HasColumnName("F工作项ID");
        builder.Property(e => e.FIssueType).HasColumnName("F问题类型").HasMaxLength(50).HasDefaultValue("");
        builder.Property(e => e.FProcessResult).HasColumnName("F处理结果").HasDefaultValue(0);
        builder.Property(e => e.FResolutionStatus).HasColumnName("F处理状态").HasMaxLength(20).HasDefaultValue("Pending");
        builder.Property(e => e.FResolutionPayloadJson).HasColumnName("F处理载荷JSON").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FResolvedBy).HasColumnName("F处理人ID");
        builder.Property(e => e.FResolvedTime).HasColumnName("F处理时间");
        builder.Property(e => e.FRetryStatus).HasColumnName("F重跑状态").HasMaxLength(20);
        builder.Property(e => e.FRetryMessage).HasColumnName("F重跑消息").HasMaxLength(500);
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId").HasDefaultValue(0L);

        builder.HasIndex(e => e.FBatchId).HasDatabaseName("IX_CF批次错误_批次");
        builder.HasIndex(e => new { e.FOrgId, e.FDispatchStatus }).HasDatabaseName("IX_CF批次错误_组织派发状态");
        builder.HasIndex(e => new { e.FOrgId, e.FResolutionStatus }).HasDatabaseName("IX_CF批次错误_组织处理状态");
    }
}
