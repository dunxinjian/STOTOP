using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfCardConfiguration : IEntityTypeConfiguration<CfCard>
{
    public void Configure(EntityTypeBuilder<CfCard> builder)
    {
        builder.ToTable("CF流程实例");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FFlowDefinitionId).HasColumnName("F流程定义ID");
        builder.Property(e => e.FFlowVersionId).HasColumnName("F流程版本ID");
        builder.Property(e => e.FCardNumber).HasColumnName("F卡片编号").HasMaxLength(200);
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30);
        builder.Property(e => e.FInitiatorId).HasColumnName("F发起人ID");
        builder.Property(e => e.FInitiatorName).HasColumnName("F发起人姓名").HasMaxLength(100);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FSubmitTime).HasColumnName("F提交时间");
        builder.Property(e => e.FCompletedTime).HasColumnName("F完成时间");
        builder.Property(e => e.FCurrentStageInstanceId).HasColumnName("F当前节点实例ID");
        builder.Property(e => e.FDataJson).HasColumnName("F数据JSON");
        builder.Property(e => e.FCurrentRound).HasColumnName("F当前轮次");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID");
        builder.Property(e => e.FOrchestrationInstanceId).HasColumnName("F编排实例ID");
        builder.Property(e => e.FOrchestrationNodeId).HasColumnName("F编排节点ID").HasMaxLength(50);
        builder.Property(e => e.FSourceModule).HasColumnName("F来源模块").HasMaxLength(60);
        builder.Property(e => e.FSourceType).HasColumnName("F来源类型").HasMaxLength(80);
        builder.Property(e => e.FSourceId).HasColumnName("F来源ID");
        builder.Property(e => e.FReturnUrl).HasColumnName("F返回地址").HasMaxLength(500);
        builder.Property(e => e.FInitialDataJson).HasColumnName("F初始数据JSON");
        builder.Property(e => e.FSourceTitle).HasColumnName("F来源标题").HasMaxLength(200);
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => e.FCardNumber).IsUnique()
            .HasFilter("[F卡片编号] IS NOT NULL")
            .HasDatabaseName("IX_CF流程实例_编号");
        builder.HasIndex(e => new { e.FOrgId, e.FStatus }).HasDatabaseName("IX_CF流程实例_组织状态");
        builder.HasIndex(e => new { e.FInitiatorId, e.FStatus }).HasDatabaseName("IX_CF流程实例_发起人");
        builder.HasIndex(e => e.FBatchId).HasFilter("[F批次ID] IS NOT NULL").HasDatabaseName("IX_CF流程实例_批次");
        builder.HasIndex(e => e.FOrchestrationInstanceId)
            .HasFilter("[F编排实例ID] IS NOT NULL")
            .HasDatabaseName("IX_CF流程实例_编排实例");
        builder.HasIndex(e => new { e.FOrgId, e.FSourceModule, e.FSourceType, e.FSourceId })
            .HasDatabaseName("IX_CF流程实例_来源");
    }
}
