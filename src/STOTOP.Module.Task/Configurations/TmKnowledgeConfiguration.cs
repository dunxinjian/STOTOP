using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmKnowledgeConfiguration : IEntityTypeConfiguration<TmKnowledge>
{
    public void Configure(EntityTypeBuilder<TmKnowledge> builder)
    {
        builder.ToTable("TM知识库");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(32).IsRequired();
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FContent).HasColumnName("F内容");
        builder.Property(e => e.FCategory).HasColumnName("F分类");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FAuthorId).HasColumnName("F作者ID");
        builder.Property(e => e.FSourceReviewId).HasColumnName("F来源复盘ID");
        builder.Property(e => e.FSourceTaskId).HasColumnName("F来源任务ID");
        builder.Property(e => e.FSourceProjectId).HasColumnName("F来源项目ID");
        builder.Property(e => e.FViewCount).HasColumnName("F浏览数").HasDefaultValue(0);
        builder.Property(e => e.FLikeCount).HasColumnName("F点赞数").HasDefaultValue(0);
        builder.Property(e => e.FCollectCount).HasColumnName("F收藏数").HasDefaultValue(0);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FIsPinned).HasColumnName("F置顶").HasDefaultValue(false);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => new { e.FOrgId, e.FCategory })
            .HasDatabaseName("IX_TM知识库_组织ID_分类");
        builder.HasIndex(e => e.FAuthorId).HasDatabaseName("IX_TM知识库_作者ID");
        builder.HasIndex(e => e.FSourceReviewId).HasDatabaseName("IX_TM知识库_来源复盘ID");

        builder.HasMany(e => e.Interactions)
            .WithOne(e => e.Knowledge)
            .HasForeignKey(e => e.FKnowledgeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Comments)
            .WithOne(e => e.Knowledge)
            .HasForeignKey(e => e.FKnowledgeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
