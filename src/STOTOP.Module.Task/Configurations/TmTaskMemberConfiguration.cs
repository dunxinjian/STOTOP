using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmTaskMemberConfiguration : IEntityTypeConfiguration<TmTaskMember>
{
    public void Configure(EntityTypeBuilder<TmTaskMember> builder)
    {
        builder.ToTable("TM任务参与者");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FTaskId).HasColumnName("F任务ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FRole).HasColumnName("F角色");

        builder.HasIndex(e => new { e.FTaskId, e.FUserId })
            .IsUnique()
            .HasDatabaseName("UQ_TM任务参与者_任务_用户");
    }
}
