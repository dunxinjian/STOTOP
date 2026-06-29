using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlReviewConfiguration : IEntityTypeConfiguration<QlReview>
{
    public void Configure(EntityTypeBuilder<QlReview> builder)
    {
        builder.ToTable("QL复盘记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FExceptionId).HasColumnName("F异常单ID");
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FRootCause).HasColumnName("F根因分析").HasMaxLength(2000);
        builder.Property(e => e.FImpactAnalysis).HasColumnName("F影响分析").HasMaxLength(2000);
        builder.Property(e => e.FConclusion).HasColumnName("F结论").HasMaxLength(2000);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FReviewDate).HasColumnName("F复盘日期");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FExceptionId).HasDatabaseName("IX_QL复盘记录_异常单ID");
        builder.HasIndex(e => new { e.FOrgId, e.FReviewDate }).HasDatabaseName("IX_QL复盘记录_组织_日期");
    }
}
