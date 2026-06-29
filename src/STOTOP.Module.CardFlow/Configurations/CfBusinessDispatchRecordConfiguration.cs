using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfBusinessDispatchRecordConfiguration : IEntityTypeConfiguration<CfBusinessDispatchRecord>
{
    public void Configure(EntityTypeBuilder<CfBusinessDispatchRecord> builder)
    {
        builder.ToTable("CF业务派发记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID").IsRequired();
        builder.Property(e => e.FBatchNo).HasColumnName("F批次号").HasMaxLength(64);
        builder.Property(e => e.FErrorId).HasColumnName("F错误ID");
        builder.Property(e => e.FDispatchType).HasColumnName("F派发方式").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FTargetType).HasColumnName("F目标类型").HasMaxLength(50);
        builder.Property(e => e.FTargetId).HasColumnName("F目标ID");
        builder.Property(e => e.FAssignee).HasColumnName("F处理人").HasMaxLength(100);
        builder.Property(e => e.FAssigneeName).HasColumnName("F处理人姓名").HasMaxLength(100);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FExceptionType).HasColumnName("F异常类型").HasMaxLength(50);
        builder.Property(e => e.FSeverityLevel).HasColumnName("F严重级别").HasMaxLength(20);
        builder.Property(e => e.FDescription).HasColumnName("F派发说明").HasMaxLength(500);
        builder.Property(e => e.FResult).HasColumnName("F处理结果").HasMaxLength(500);
        builder.Property(e => e.FDeadline).HasColumnName("F截止时间");
        builder.Property(e => e.FCompletedTime).HasColumnName("F完成时间");
        builder.Property(e => e.FCreator).HasColumnName("F创建人").HasMaxLength(100);
        builder.Property(e => e.FOperator).HasColumnName("F操作人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId").HasDefaultValue(0L);

        builder.HasIndex(e => e.FBatchId).HasDatabaseName("IX_CF业务派发记录_批次");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_CF业务派发记录_状态");
        builder.HasIndex(e => e.FAssignee).HasDatabaseName("IX_CF业务派发记录_处理人");
    }
}
