using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfFlowDefinitionConfiguration : IEntityTypeConfiguration<CfFlowDefinition>
{
    public void Configure(EntityTypeBuilder<CfFlowDefinition> builder)
    {
        builder.ToTable("CF卡片流程");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FFlowName).HasColumnName("F流程名称").HasMaxLength(100);
        builder.Property(e => e.FFlowCode).HasColumnName("F流程编码").HasMaxLength(50);
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30);
        builder.Property(e => e.FNumberTemplate).HasColumnName("F编号模板").HasMaxLength(200);
        builder.Property(e => e.FTitleTemplate).HasColumnName("F标题模板").HasMaxLength(200);
        builder.Property(e => e.FAllowedRolesJson).HasColumnName("F可发起角色JSON");
        builder.Property(e => e.FFlowGroupId).HasColumnName("F流程组ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FTriggerConfigJson).HasColumnName("F触发配置JSON");
        builder.Property(e => e.FMatchPattern).HasColumnName("F匹配规则").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FIsTemplate).HasColumnName("F是否模板").HasDefaultValue(false);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => new { e.FFlowCode, e.FOrgId }).IsUnique().HasDatabaseName("IX_CF卡片流程_编码_组织");
        builder.HasIndex(e => new { e.FOrgId, e.FStatus }).HasDatabaseName("IX_CF卡片流程_组织状态");
    }
}
