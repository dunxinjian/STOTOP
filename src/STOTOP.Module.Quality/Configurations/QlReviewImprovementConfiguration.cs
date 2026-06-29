using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlReviewImprovementConfiguration : IEntityTypeConfiguration<QlReviewImprovement>
{
    public void Configure(EntityTypeBuilder<QlReviewImprovement> builder)
    {
        builder.ToTable("QL改进措施");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FReviewId).HasColumnName("F复盘ID");
        builder.Property(e => e.FContent).HasColumnName("F内容").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FAssigneeId).HasColumnName("F负责人ID");
        builder.Property(e => e.FDeadline).HasColumnName("F截止时间");
        builder.Property(e => e.FCompleted).HasColumnName("F已完成");
        builder.Property(e => e.FCompletedTime).HasColumnName("F完成时间");
        builder.Property(e => e.FSortOrder).HasColumnName("F排序");

        builder.HasIndex(e => e.FReviewId).HasDatabaseName("IX_QL改进措施_复盘ID");
    }
}
