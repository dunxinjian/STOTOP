using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfFlowGroupConfiguration : IEntityTypeConfiguration<CfFlowGroup>
{
    public void Configure(EntityTypeBuilder<CfFlowGroup> builder)
    {
        builder.ToTable("CF流程组");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FGroupName).HasColumnName("F流程组名称").HasMaxLength(100);
        builder.Property(e => e.FGroupCode).HasColumnName("F流程组编码").HasMaxLength(50);
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FGroupCode).IsUnique().HasDatabaseName("IX_CF流程组_编码");
    }
}
