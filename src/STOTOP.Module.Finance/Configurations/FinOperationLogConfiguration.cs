using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinOperationLogConfiguration : IEntityTypeConfiguration<FinOperationLog>
{
    public void Configure(EntityTypeBuilder<FinOperationLog> builder)
    {
        builder.ToTable("FIN操作日志");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID").HasDefaultValue(0L);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FModule).HasColumnName("F模块").HasMaxLength(50);
        builder.Property(e => e.FOperationType).HasColumnName("F操作类型").HasMaxLength(50);
        builder.Property(e => e.FDescription).HasColumnName("F操作描述").HasMaxLength(500);
        builder.Property(e => e.FTargetId).HasColumnName("F目标ID");
        builder.Property(e => e.FTargetCode).HasColumnName("F目标编号").HasMaxLength(100);
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FOperatorName).HasColumnName("F操作人").HasMaxLength(100);
        builder.Property(e => e.FOperationTime).HasColumnName("F操作时间");
        builder.Property(e => e.FIpAddress).HasColumnName("FIP地址").HasMaxLength(50);
        builder.Property(e => e.FExtraData).HasColumnName("F扩展数据");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN操作日志_账套ID");
        builder.HasIndex(e => e.FOperationTime).HasDatabaseName("IX_FIN操作日志_操作时间");
        builder.HasIndex(e => new { e.FAccountSetId, e.FModule, e.FOperationType }).HasDatabaseName("IX_FIN操作日志_账套模块类型");
    }
}
