using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Configurations;

public class WfChainCommentConfiguration : IEntityTypeConfiguration<WfChainComment>
{
    public void Configure(EntityTypeBuilder<WfChainComment> builder)
    {
        builder.ToTable("WF链路评论");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FChainId).HasColumnName("F链路ID").HasMaxLength(64).IsRequired();
        builder.Property(e => e.FWorkItemId).HasColumnName("F工作项ID");
        builder.Property(e => e.FAuthorId).HasColumnName("F作者ID");
        builder.Property(e => e.FAuthorName).HasColumnName("F作者姓名").HasMaxLength(50);
        builder.Property(e => e.FContent).HasColumnName("F内容").IsRequired();
        builder.Property(e => e.FAttachments).HasColumnName("F附件");
        builder.Property(e => e.FReplyToId).HasColumnName("F回复目标ID");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间");
        builder.Property(e => e.FIsDeleted).HasColumnName("F已删除").HasDefaultValue(false);

        // 索引
        builder.HasIndex(e => e.FChainId).HasDatabaseName("IX_WF链路评论_链路ID");
        builder.HasIndex(e => e.FWorkItemId).HasDatabaseName("IX_WF链路评论_工作项ID");

        // 关系
        builder.HasOne(e => e.ReplyTo)
            .WithMany()
            .HasForeignKey(e => e.FReplyToId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
