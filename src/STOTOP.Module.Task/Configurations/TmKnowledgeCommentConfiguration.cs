using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmKnowledgeCommentConfiguration : IEntityTypeConfiguration<TmKnowledgeComment>
{
    public void Configure(EntityTypeBuilder<TmKnowledgeComment> builder)
    {
        builder.ToTable("TM知识评论");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FKnowledgeId).HasColumnName("F知识ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FContent).HasColumnName("F内容");
        builder.Property(e => e.FParentCommentId).HasColumnName("F父评论ID").HasDefaultValue(0L);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FKnowledgeId).HasDatabaseName("IX_TM知识评论_知识ID");
    }
}
