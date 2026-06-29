using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfBatchConfiguration : IEntityTypeConfiguration<CfBatch>
{
    public void Configure(EntityTypeBuilder<CfBatch> builder)
    {
        builder.ToTable("CF批次");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FFlowDefinitionId).HasColumnName("F流程定义ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTriggeredById).HasColumnName("F触发人ID");
        builder.Property(e => e.FTriggeredTime).HasColumnName("F触发时间");
        builder.Property(e => e.FTriggerType).HasColumnName("F触发类型").HasMaxLength(30);
        builder.Property(e => e.FFilePath).HasColumnName("F文件路径").HasMaxLength(500);
        builder.Property(e => e.FFileName).HasColumnName("F原始文件名").HasMaxLength(500);
        builder.Property(e => e.FSkipRows).HasColumnName("F跳过行数").HasDefaultValue(0);
        builder.Property(e => e.FColumnMappingJson).HasColumnName("F列映射JSON");
        builder.Property(e => e.FTotalRows).HasColumnName("F总行数");
        builder.Property(e => e.FSuccessRows).HasColumnName("F成功行数");
        builder.Property(e => e.FFailedRows).HasColumnName("F失败行数");
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FIsRevoked).HasColumnName("F已撤销");
        builder.Property(e => e.FRevokedTime).HasColumnName("F撤销时间");
        builder.Property(e => e.FRevokedById).HasColumnName("F撤销人ID");
        builder.Property(e => e.FErrorMessage).HasColumnName("F错误信息").HasMaxLength(2000);
        builder.Property(e => e.FOrchestrationInstanceId).HasColumnName("F编排实例ID");
        builder.Property(e => e.FOrchestrationNodeId).HasColumnName("F编排节点ID").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        builder.Property(e => e.FBatchNo).HasColumnName("F批次号").HasMaxLength(64);
        builder.Property(e => e.FFileHash).HasColumnName("F文件哈希").HasMaxLength(64);
        builder.Property(e => e.FFileSize).HasColumnName("F文件大小");
        builder.Property(e => e.FImportStartTime).HasColumnName("F导入开始时间");
        builder.Property(e => e.FImportEndTime).HasColumnName("F导入结束时间");
        builder.Property(e => e.FCurrentNodeName).HasColumnName("F当前节点名称").HasMaxLength(200);
        builder.Property(e => e.FChangeVersion).HasColumnName("F变更版本号").HasDefaultValue(0L);
        builder.Property(e => e.FProgressPercent).HasColumnName("F进度百分比");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FUploadMethod).HasColumnName("F上传方式").HasMaxLength(20);
        builder.Property(e => e.FWorkItemId).HasColumnName("F工作项ID");
        builder.Property(e => e.FActualTargetTable).HasColumnName("F实际暂存表").HasMaxLength(200);
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => new { e.FOrgId, e.FStatus }).HasDatabaseName("IX_CF批次_组织状态");
        builder.HasIndex(e => e.FFlowDefinitionId).HasDatabaseName("IX_CF批次_流程定义");
        builder.HasIndex(e => e.FTriggeredTime).HasDatabaseName("IX_CF批次_触发时间");
        builder.HasIndex(e => e.FOrchestrationInstanceId)
            .HasFilter("[F编排实例ID] IS NOT NULL")
            .HasDatabaseName("IX_CF批次_编排实例");
        builder.HasIndex(e => e.FBatchNo)
            .HasFilter("[F批次号] IS NOT NULL")
            .HasDatabaseName("IX_CF批次_批次号");
        builder.HasIndex(e => e.FChangeVersion).HasDatabaseName("IX_CF批次_变更版本");
    }
}
