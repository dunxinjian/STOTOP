using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmTaskVisibilityConfiguration : IEntityTypeConfiguration<TmTaskVisibility>
{
    public void Configure(EntityTypeBuilder<TmTaskVisibility> builder)
    {
        builder.ToTable("TM任务可见范围");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID");
        builder.Property(e => e.FTargetType).HasColumnName("F目标类型");
        builder.Property(e => e.FTargetId).HasColumnName("F目标ID");

        builder.HasIndex(e => e.FTaskId).HasDatabaseName("IX_TM任务可见范围_任务ID");
    }
}
