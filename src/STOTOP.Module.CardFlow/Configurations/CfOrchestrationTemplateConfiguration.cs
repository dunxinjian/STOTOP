using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfOrchestrationTemplateConfiguration : IEntityTypeConfiguration<CfOrchestrationTemplate>
{
    public void Configure(EntityTypeBuilder<CfOrchestrationTemplate> builder)
    {
        builder.ToTable("CF编排模板");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FNodesJson).HasColumnName("F节点JSON");
        builder.Property(e => e.FEdgesJson).HasColumnName("F边JSON");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(20);
        builder.Property(e => e.FMaxTriggerCount).HasColumnName("F最大触发次数");
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F修改时间");
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => new { e.FCode, e.FOrgId })
            .IsUnique()
            .HasDatabaseName("IX_CF编排模板_编码_组织");
        builder.HasIndex(e => new { e.FOrgId, e.FStatus })
            .HasDatabaseName("IX_CF编排模板_组织状态");
    }
}
