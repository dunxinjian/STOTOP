using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmTaskDependencyConfiguration : IEntityTypeConfiguration<TmTaskDependency>
{
    public void Configure(EntityTypeBuilder<TmTaskDependency> builder)
    {
        builder.ToTable("TM任务依赖");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID");
        builder.Property(e => e.FDependsOnTaskId).HasColumnName("F依赖任务ID");
        builder.Property(e => e.FDependencyType).HasColumnName("F依赖类型").HasDefaultValue(0);

        builder.HasIndex(e => new { e.FTaskId, e.FDependsOnTaskId })
            .IsUnique()
            .HasDatabaseName("UQ_TM任务依赖_任务_依赖");

        builder.HasOne(e => e.DependsOnTask)
            .WithMany()
            .HasForeignKey(e => e.FDependsOnTaskId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
