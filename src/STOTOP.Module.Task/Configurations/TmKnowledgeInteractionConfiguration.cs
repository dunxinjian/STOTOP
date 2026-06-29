using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmKnowledgeInteractionConfiguration : IEntityTypeConfiguration<TmKnowledgeInteraction>
{
    public void Configure(EntityTypeBuilder<TmKnowledgeInteraction> builder)
    {
        builder.ToTable("TM知识互动");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FKnowledgeId).HasColumnName("F知识ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FInteractionType).HasColumnName("F互动类型");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FKnowledgeId, e.FUserId, e.FInteractionType })
            .IsUnique()
            .HasDatabaseName("UQ_TM知识互动_知识_用户_类型");
    }
}
