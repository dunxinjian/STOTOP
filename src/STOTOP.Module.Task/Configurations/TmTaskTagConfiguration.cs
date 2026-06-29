using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmTaskTagConfiguration : IEntityTypeConfiguration<TmTaskTag>
{
    public void Configure(EntityTypeBuilder<TmTaskTag> builder)
    {
        builder.ToTable("TM任务标签");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID");
        builder.Property(e => e.FTagId).HasColumnName("F标签ID");

        builder.HasIndex(e => new { e.FTaskId, e.FTagId })
            .IsUnique()
            .HasDatabaseName("UQ_TM任务标签_任务_标签");
    }
}
