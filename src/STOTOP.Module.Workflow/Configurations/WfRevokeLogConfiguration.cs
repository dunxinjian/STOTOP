using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Configurations;

public class WfRevokeLogConfiguration : IEntityTypeConfiguration<WfRevokeLog>
{
    public void Configure(EntityTypeBuilder<WfRevokeLog> builder)
    {
        builder.ToTable("WF撤销日志");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FChainId).HasColumnName("F链路ID").HasMaxLength(64);
        builder.Property(e => e.FDataScopeId).HasColumnName("F数据作用域ID").HasMaxLength(64);
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FOperatorName).HasColumnName("F操作人姓名").HasMaxLength(50);
        builder.Property(e => e.FRevokeType).HasColumnName("F撤销类型").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FTargetTable).HasColumnName("F目标表").HasMaxLength(100);
        builder.Property(e => e.FAffectedRows).HasColumnName("F影响行数").HasDefaultValue(0);
        builder.Property(e => e.FRevokeStrategy).HasColumnName("F撤销策略").HasMaxLength(50);
        builder.Property(e => e.FSnapshot).HasColumnName("F快照");
        builder.Property(e => e.FIsSuccess).HasColumnName("F是否成功").HasDefaultValue(true);
        builder.Property(e => e.FErrorMessage).HasColumnName("F错误信息").HasMaxLength(2000);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间");

        // 索引
        builder.HasIndex(e => e.FChainId).HasDatabaseName("IX_WF撤销日志_链路ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_WF撤销日志_数据作用域");
    }
}
