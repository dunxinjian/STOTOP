using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfNumberSequenceConfiguration : IEntityTypeConfiguration<CfNumberSequence>
{
    public void Configure(EntityTypeBuilder<CfNumberSequence> builder)
    {
        builder.ToTable("CF编号序号");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FFlowDefinitionId).HasColumnName("F流程定义ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FYear).HasColumnName("F年份");
        builder.Property(e => e.FCurrentSequence).HasColumnName("F当前序号");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => new { e.FFlowDefinitionId, e.FOrgId, e.FYear })
            .IsUnique()
            .HasDatabaseName("IX_CF编号序号_唯一");
    }
}
