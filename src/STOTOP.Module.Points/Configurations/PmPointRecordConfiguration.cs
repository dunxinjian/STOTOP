using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Configurations;

public class PmPointRecordConfiguration : IEntityTypeConfiguration<PmPointRecord>
{
    public void Configure(EntityTypeBuilder<PmPointRecord> builder)
    {
        builder.ToTable("PM积分记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FSourceId).HasColumnName("F来源ID");
        builder.Property(e => e.FRuleId).HasColumnName("F规则ID");
        builder.Property(e => e.FType).HasColumnName("F类型");
        builder.Property(e => e.FPointValue).HasColumnName("F积分值");
        builder.Property(e => e.FBalance).HasColumnName("F余额");
        builder.Property(e => e.FRelatedModule).HasColumnName("F关联模块").HasMaxLength(30);
        builder.Property(e => e.FRelatedEntityType).HasColumnName("F关联实体类型").HasMaxLength(30);
        builder.Property(e => e.FRelatedEntityId).HasColumnName("F关联实体ID");
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500).IsRequired();
        builder.Property(e => e.F账户类型).HasColumnName("F账户类型").HasDefaultValue(1);
        builder.Property(e => e.F关联事件类型).HasColumnName("F关联事件类型").HasMaxLength(64);
        builder.Property(e => e.F关联事件ID).HasColumnName("F关联事件ID").HasMaxLength(64);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUserId).HasDatabaseName("IX_PM积分记录_用户ID");
        builder.HasIndex(e => new { e.FOrgId, e.FCreateTime }).HasDatabaseName("IX_PM积分记录_组织_时间");
        builder.HasIndex(e => new { e.FRelatedModule, e.FRelatedEntityType, e.FRelatedEntityId }).HasDatabaseName("IX_PM积分记录_关联");
        // 事件幂等过滤唯一索引：仅在 F关联事件ID IS NOT NULL 时生效
        builder.HasIndex(e => new { e.FOrgId, e.F关联事件类型, e.F关联事件ID, e.F账户类型 })
            .IsUnique()
            .HasFilter("[F关联事件ID] IS NOT NULL")
            .HasDatabaseName("UQ_PM积分记录_事件幂等");
    }
}
