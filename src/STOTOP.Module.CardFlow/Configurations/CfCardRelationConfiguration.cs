using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfCardRelationConfiguration : IEntityTypeConfiguration<CfCardRelation>
{
    public void Configure(EntityTypeBuilder<CfCardRelation> builder)
    {
        builder.ToTable("CF卡片关联");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FSourceCardId).HasColumnName("F源卡片ID");
        builder.Property(e => e.FTargetCardId).HasColumnName("F目标卡片ID");
        builder.Property(e => e.FRelationType).HasColumnName("F关联类型").HasMaxLength(30);
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.FOffsetAmount).HasColumnName("F冲销金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FSnapshotDataJson).HasColumnName("F快照数据JSON");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");

        builder.HasIndex(e => e.FSourceCardId).HasDatabaseName("IX_CF卡片关联_源");
        builder.HasIndex(e => e.FTargetCardId).HasDatabaseName("IX_CF卡片关联_目标");
    }
}
