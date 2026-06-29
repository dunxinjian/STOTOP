using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlKnowledgeConfiguration : IEntityTypeConfiguration<QlKnowledge>
{
    public void Configure(EntityTypeBuilder<QlKnowledge> builder)
    {
        builder.ToTable("QL知识库");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FContent).HasColumnName("F内容").IsRequired();
        builder.Property(e => e.FCategory).HasColumnName("F分类").HasMaxLength(50);
        builder.Property(e => e.FTags).HasColumnName("F标签").HasMaxLength(200);
        builder.Property(e => e.FRelatedExceptionId).HasColumnName("F关联异常单ID");
        builder.Property(e => e.FRelatedReviewId).HasColumnName("F关联复盘ID");
        builder.Property(e => e.FViewCount).HasColumnName("F浏览量");
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.FCategory }).HasDatabaseName("IX_QL知识库_组织_分类");
    }
}
