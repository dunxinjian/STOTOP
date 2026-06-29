using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Configurations;

public class TmProjectMemberConfiguration : IEntityTypeConfiguration<TmProjectMember>
{
    public void Configure(EntityTypeBuilder<TmProjectMember> builder)
    {
        builder.ToTable("TM项目成员");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FProjectId).HasColumnName("F项目ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FRole).HasColumnName("F角色");
        builder.Property(e => e.FJoinTime).HasColumnName("F加入时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FProjectId, e.FUserId })
            .IsUnique()
            .HasDatabaseName("UQ_TM项目成员_项目_用户");
    }
}
