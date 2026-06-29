using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Configurations;

public class WfIssueDetailConfiguration : IEntityTypeConfiguration<WfIssueDetail>
{
    public void Configure(EntityTypeBuilder<WfIssueDetail> builder)
    {
        builder.ToTable("WF问题明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FIssuePackId).HasColumnName("F问题包ID");
        builder.Property(e => e.FDataScopeId).HasColumnName("F数据作用域ID").HasMaxLength(64);
        builder.Property(e => e.FRowId).HasColumnName("F数据行ID");
        builder.Property(e => e.FTableName).HasColumnName("F表名").HasMaxLength(100);
        builder.Property(e => e.FErrorType).HasColumnName("F错误类型").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FErrorMessage).HasColumnName("F错误信息").HasMaxLength(2000);
        builder.Property(e => e.FFieldName).HasColumnName("F字段名").HasMaxLength(100);
        builder.Property(e => e.FOriginalValue).HasColumnName("F原始值").HasMaxLength(500);
        builder.Property(e => e.FCorrectedValue).HasColumnName("F修正值").HasMaxLength(500);
        builder.Property(e => e.FIsResolved).HasColumnName("F是否已解决").HasDefaultValue(false);
        builder.Property(e => e.FResolverId).HasColumnName("F解决人ID");
        builder.Property(e => e.FResolvedTime).HasColumnName("F解决时间");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间");

        // 索引
        builder.HasIndex(e => e.FIssuePackId).HasDatabaseName("IX_WF问题明细_问题包ID");
        builder.HasIndex(e => e.FIsResolved).HasDatabaseName("IX_WF问题明细_已解决");
    }
}
