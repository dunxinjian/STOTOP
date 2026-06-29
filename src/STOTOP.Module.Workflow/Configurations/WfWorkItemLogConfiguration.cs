using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Configurations;

public class WfWorkItemLogConfiguration : IEntityTypeConfiguration<WfWorkItemLog>
{
    public void Configure(EntityTypeBuilder<WfWorkItemLog> builder)
    {
        builder.ToTable("WF工作项日志");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FWorkItemId).HasColumnName("F工作项ID");
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FOperatorName).HasColumnName("F操作人姓名").HasMaxLength(50);
        builder.Property(e => e.FAction).HasColumnName("F操作类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FFromStatus).HasColumnName("F变更前状态");
        builder.Property(e => e.FToStatus).HasColumnName("F变更后状态");
        builder.Property(e => e.FContent).HasColumnName("F操作内容").HasMaxLength(2000);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间");

        // 索引
        builder.HasIndex(e => e.FWorkItemId).HasDatabaseName("IX_WF工作项日志_工作项ID");
    }
}
