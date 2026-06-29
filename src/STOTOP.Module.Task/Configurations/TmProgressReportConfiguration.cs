using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmProgressReportConfiguration : IEntityTypeConfiguration<TmProgressReport>
{
    public void Configure(EntityTypeBuilder<TmProgressReport> builder)
    {
        builder.ToTable("TM进度上报");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID");
        builder.Property(e => e.FReporterId).HasColumnName("F上报人ID");
        builder.Property(e => e.FProgress).HasColumnName("F进度");
        builder.Property(e => e.FContent).HasColumnName("F上报内容");
        builder.Property(e => e.FHours).HasColumnName("F工时").HasColumnType("decimal(10,1)");
        builder.Property(e => e.FPushedToDingTalk).HasColumnName("F已推送钉钉").HasDefaultValue(false);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FTaskId).HasDatabaseName("IX_TM进度上报_任务ID");
        builder.HasIndex(e => e.FReporterId).HasDatabaseName("IX_TM进度上报_上报人ID");
    }
}
