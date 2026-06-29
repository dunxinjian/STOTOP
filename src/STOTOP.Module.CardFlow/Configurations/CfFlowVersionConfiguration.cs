using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfFlowVersionConfiguration : IEntityTypeConfiguration<CfFlowVersion>
{
    public void Configure(EntityTypeBuilder<CfFlowVersion> builder)
    {
        builder.ToTable("CF流程版本");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FFlowDefinitionId).HasColumnName("F流程定义ID");
        builder.Property(e => e.FVersionNumber).HasColumnName("F版本号");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30);
        builder.Property(e => e.FCardSchemaJson).HasColumnName("F卡片SchemaJSON");
        builder.Property(e => e.FDetailSchemaJson).HasColumnName("F明细SchemaJSON");
        builder.Property(e => e.FFlowSettingsJson).HasColumnName("F流程设置JSON");
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FPublishTime).HasColumnName("F发布时间");
        builder.Property(e => e.FIsCurrentVersion).HasColumnName("F是否当前版本");

        builder.HasIndex(e => e.FFlowDefinitionId).HasDatabaseName("IX_CF流程版本_流程定义");
        builder.HasIndex(e => new { e.FFlowDefinitionId, e.FIsCurrentVersion })
            .HasFilter("[F是否当前版本] = 1")
            .HasDatabaseName("IX_CF流程版本_当前版本");
    }
}
