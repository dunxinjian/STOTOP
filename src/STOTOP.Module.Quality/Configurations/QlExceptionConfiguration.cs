using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlExceptionConfiguration : IEntityTypeConfiguration<QlException>
{
    public void Configure(EntityTypeBuilder<QlException> builder)
    {
        builder.ToTable("QL异常单");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FExceptionNo).HasColumnName("F异常编号").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.FType).HasColumnName("F类型");
        builder.Property(e => e.FStatus).HasColumnName("F状态");
        builder.Property(e => e.FPriority).HasColumnName("F优先级");
        builder.Property(e => e.FRuleId).HasColumnName("F规则ID");
        builder.Property(e => e.FSource).HasColumnName("F来源").HasMaxLength(100);
        builder.Property(e => e.FRelatedModule).HasColumnName("F关联模块").HasMaxLength(50);
        builder.Property(e => e.FRelatedEntityId).HasColumnName("F关联实体ID");
        builder.Property(e => e.FAssigneeId).HasColumnName("F负责人ID");
        builder.Property(e => e.FDispatchMethod).HasColumnName("F派发方式");
        builder.Property(e => e.FDispatchEntityId).HasColumnName("F派发实体ID");
        builder.Property(e => e.FDeadline).HasColumnName("F截止时间");
        builder.Property(e => e.FClosedTime).HasColumnName("F关闭时间");
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.FStatus }).HasDatabaseName("IX_QL异常单_组织_状态");
        builder.HasIndex(e => e.FAssigneeId).HasDatabaseName("IX_QL异常单_负责人");
        builder.HasIndex(e => e.FCreateTime).HasDatabaseName("IX_QL异常单_创建时间");
    }
}
