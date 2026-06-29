using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfFlowGroupLinkConfiguration : IEntityTypeConfiguration<CfFlowGroupLink>
{
    public void Configure(EntityTypeBuilder<CfFlowGroupLink> builder)
    {
        builder.ToTable("CF流程组连接");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FFlowGroupId).HasColumnName("F流程组ID");
        builder.Property(e => e.FSourceFlowId).HasColumnName("F源流程定义ID");
        builder.Property(e => e.FTargetFlowId).HasColumnName("F目标流程定义ID");
        builder.Property(e => e.FTriggerCondition).HasColumnName("F触发条件").HasMaxLength(500);
        builder.Property(e => e.FFieldMappingJson).HasColumnName("F字段映射JSON");
        builder.Property(e => e.FTriggerMode).HasColumnName("F触发方式").HasMaxLength(30);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序号");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FFlowGroupId).HasDatabaseName("IX_CF流程组连接_组");
        builder.HasIndex(e => e.FSourceFlowId).HasDatabaseName("IX_CF流程组连接_源");
    }
}
