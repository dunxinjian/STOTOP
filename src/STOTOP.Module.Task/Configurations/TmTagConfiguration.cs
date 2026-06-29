using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmTagConfiguration : IEntityTypeConfiguration<TmTag>
{
    public void Configure(EntityTypeBuilder<TmTag> builder)
    {
        builder.ToTable("TM标签");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FColor).HasColumnName("F颜色").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FSort).HasColumnName("F排序").HasDefaultValue(0);

        builder.HasIndex(e => new { e.FOrgId, e.FName })
            .IsUnique()
            .HasDatabaseName("UQ_TM标签_组织_名称");

        builder.HasMany(e => e.TaskTags)
            .WithOne(e => e.Tag)
            .HasForeignKey(e => e.FTagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
