using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfTriggerEventQueueConfiguration : IEntityTypeConfiguration<CfTriggerEventQueue>
{
    public void Configure(EntityTypeBuilder<CfTriggerEventQueue> builder)
    {
        builder.ToTable("CF触发事件队列");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FFlowDefinitionId).HasColumnName("F流程定义ID");
        builder.Property(e => e.FEventType).HasColumnName("F事件类型").HasMaxLength(50);
        builder.Property(e => e.FEventDataJson).HasColumnName("F事件数据JSON");
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FProcessedTime).HasColumnName("F处理时间");

        builder.HasIndex(e => new { e.FFlowDefinitionId, e.FStatus }).HasDatabaseName("IX_CF触发事件队列_流程状态");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_CF触发事件队列_状态");
    }
}
