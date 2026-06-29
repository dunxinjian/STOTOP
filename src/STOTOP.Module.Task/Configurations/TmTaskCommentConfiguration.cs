using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmTaskCommentConfiguration : IEntityTypeConfiguration<TmTaskComment>
{
    public void Configure(EntityTypeBuilder<TmTaskComment> builder)
    {
        builder.ToTable("TM任务评论");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FContent).HasColumnName("F内容");
        builder.Property(e => e.FType).HasColumnName("F类型").HasDefaultValue(0);
        builder.Property(e => e.FParentCommentId).HasColumnName("F父评论ID").HasDefaultValue(0L);
        builder.Property(e => e.FPushedToDingTalk).HasColumnName("F已推送钉钉").HasDefaultValue(false);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FTaskId).HasDatabaseName("IX_TM任务评论_任务ID");
        builder.HasIndex(e => e.FParentCommentId).HasDatabaseName("IX_TM任务评论_父评论ID");

        builder.HasMany(e => e.Reactions)
            .WithOne(e => e.Comment)
            .HasForeignKey(e => e.FCommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
