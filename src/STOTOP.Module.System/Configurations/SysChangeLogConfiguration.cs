using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysChangeLogConfiguration : IEntityTypeConfiguration<SysChangeLog>
{
    public void Configure(EntityTypeBuilder<SysChangeLog> builder)
    {
        builder.ToTable("SYS变更记录");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBusinessType).HasColumnName("F业务类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FBusinessId).HasColumnName("F业务ID");
        builder.Property(e => e.FBusinessName).HasColumnName("F业务名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FOperationType).HasColumnName("F操作类型").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FChangeContent).HasColumnName("F变更内容").IsRequired();
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FOperatorName).HasColumnName("F操作人姓名").HasMaxLength(100);
        builder.Property(e => e.FOperationTime).HasColumnName("F操作时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FDingTalkSyncStatus).HasColumnName("F钉钉同步状态").HasDefaultValue(0);
        builder.Property(e => e.FDingTalkSyncTime).HasColumnName("F钉钉同步时间");
        builder.Property(e => e.FDingTalkSyncResult).HasColumnName("F钉钉同步结果").HasMaxLength(500);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);

        builder.HasIndex(e => new { e.FBusinessType, e.FBusinessId }).HasDatabaseName("IX_SYS变更记录_业务");
        builder.HasIndex(e => e.FOperationTime).IsDescending().HasDatabaseName("IX_SYS变更记录_操作时间");
        builder.HasIndex(e => e.FOperatorId).HasDatabaseName("IX_SYS变更记录_操作人");
    }
}
