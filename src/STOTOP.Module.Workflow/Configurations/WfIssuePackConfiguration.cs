using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Configurations;

public class WfIssuePackConfiguration : IEntityTypeConfiguration<WfIssuePack>
{
    public void Configure(EntityTypeBuilder<WfIssuePack> builder)
    {
        builder.ToTable("WF问题包");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(32).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FChainId).HasColumnName("F链路ID").HasMaxLength(64);
        builder.Property(e => e.FWorkItemId).HasColumnName("F工作项ID");
        builder.Property(e => e.FIssueType).HasColumnName("F问题类型").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FBatchId).HasColumnName("F批次ID");
        builder.Property(e => e.FTotalCount).HasColumnName("F问题总数").HasDefaultValue(0);
        builder.Property(e => e.FResolvedCount).HasColumnName("F已解决数").HasDefaultValue(0);
        builder.Property(e => e.FSummary).HasColumnName("F摘要").HasMaxLength(2000);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间");

        // 索引
        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => e.FChainId).HasDatabaseName("IX_WF问题包_链路ID");
        builder.HasIndex(e => e.FWorkItemId).HasDatabaseName("IX_WF问题包_工作项ID");
        builder.HasIndex(e => e.FBatchId).HasDatabaseName("IX_WF问题包_批次ID");

        // 关系
        builder.HasOne(e => e.WorkItem)
            .WithMany()
            .HasForeignKey(e => e.FWorkItemId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.Details)
            .WithOne(e => e.IssuePack)
            .HasForeignKey(e => e.FIssuePackId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
