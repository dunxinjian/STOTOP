using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmCommentReactionConfiguration : IEntityTypeConfiguration<TmCommentReaction>
{
    public void Configure(EntityTypeBuilder<TmCommentReaction> builder)
    {
        builder.ToTable("TM评论表情");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCommentId).HasColumnName("F评论ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FEmojiCode).HasColumnName("F表情编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FCommentId, e.FUserId, e.FEmojiCode })
            .IsUnique()
            .HasDatabaseName("UQ_TM评论表情_评论_用户_表情");
    }
}
