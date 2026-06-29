using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmReviewRecordConfiguration : IEntityTypeConfiguration<TmReviewRecord>
{
    public void Configure(EntityTypeBuilder<TmReviewRecord> builder)
    {
        builder.ToTable("TM复盘记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(32).IsRequired();
        builder.Property(e => e.FRelationType).HasColumnName("F关联类型");
        builder.Property(e => e.FRelationId).HasColumnName("F关联ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FWentWell).HasColumnName("F做得好的");
        builder.Property(e => e.FToImprove).HasColumnName("F待改进的");
        builder.Property(e => e.FLessonsLearned).HasColumnName("F经验方法");
        builder.Property(e => e.FActionPlan).HasColumnName("F行动计划");
        builder.Property(e => e.FReviewerId).HasColumnName("F复盘人ID");
        builder.Property(e => e.FParticipantIds).HasColumnName("F参与人IDs").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUID).IsUnique();
        builder.HasIndex(e => new { e.FRelationType, e.FRelationId })
            .HasDatabaseName("IX_TM复盘记录_关联");
        builder.HasIndex(e => e.FReviewerId).HasDatabaseName("IX_TM复盘记录_复盘人ID");
        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_TM复盘记录_组织ID");
    }
}
