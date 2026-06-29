using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfStageDefinitionConfiguration : IEntityTypeConfiguration<CfStageDefinition>
{
    public void Configure(EntityTypeBuilder<CfStageDefinition> builder)
    {
        builder.ToTable("CF流程节点");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FFlowVersionId).HasColumnName("F流程版本ID");
        builder.Property(e => e.FStageKey).HasColumnName("F节点键").HasMaxLength(80);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序号");
        builder.Property(e => e.FStageName).HasColumnName("F节点名称").HasMaxLength(100);
        builder.Property(e => e.FType).HasColumnName("F类型").HasMaxLength(30);
        builder.Property(e => e.F处理粒度).HasColumnName("F处理粒度").HasMaxLength(20).HasDefaultValue("card");
        builder.Property(e => e.FApprovalMode).HasColumnName("F审批模式").HasMaxLength(30);
        builder.Property(e => e.FAssigneeStrategy).HasColumnName("F处理人策略").HasMaxLength(30);
        builder.Property(e => e.FAssigneeConfigJson).HasColumnName("F处理人配置JSON");
        builder.Property(e => e.FConditionJson).HasColumnName("F进入条件JSON");
        builder.Property(e => e.FInputFieldsJson).HasColumnName("F补充字段JSON");
#pragma warning disable CS0618 // 旧版字段过渡期内保留映射，由 Task #3 插件适配完成后移除
        builder.Property(e => e.FAutoPluginName).HasColumnName("F自动AutoPlugin名称").HasMaxLength(100);
        builder.Property(e => e.FAutoPluginConfigJson).HasColumnName("FAutoPlugin配置JSON");
#pragma warning restore CS0618
        builder.Property(e => e.F插件注册ID).HasColumnName("F插件注册ID");
        builder.Property(e => e.F插件规则ID).HasColumnName("F插件规则ID");
        builder.Property(e => e.FFailurePolicyJson).HasColumnName("F失败策略JSON");
        builder.Property(e => e.FCcConfigJson).HasColumnName("F抄送配置JSON");
        builder.Property(e => e.FTimeoutHours).HasColumnName("F超时小时数");
        builder.Property(e => e.FPriorityTemplate).HasColumnName("F优先级模板");

        builder.HasIndex(e => new { e.FFlowVersionId, e.FSortOrder }).HasDatabaseName("IX_CF流程节点_版本排序");
        builder.HasIndex(e => new { e.FFlowVersionId, e.FStageKey })
            .IsUnique()
            .HasDatabaseName("IX_CF流程节点_版本节点键");
    }
}
