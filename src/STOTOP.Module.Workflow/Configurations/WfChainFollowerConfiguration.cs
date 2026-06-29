using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Configurations;

public class WfChainFollowerConfiguration : IEntityTypeConfiguration<WfChainFollower>
{
    public void Configure(EntityTypeBuilder<WfChainFollower> builder)
    {
        builder.ToTable("WF链路关注");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FChainId).HasColumnName("F链路ID").HasMaxLength(64).IsRequired();
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FUserName).HasColumnName("F用户姓名").HasMaxLength(50);
        builder.Property(e => e.FFollowTime).HasColumnName("F关注时间");
        builder.Property(e => e.FIsMuted).HasColumnName("F已静音").HasDefaultValue(false);

        // 索引
        builder.HasIndex(e => e.FChainId).HasDatabaseName("IX_WF链路关注_链路ID");
        builder.HasIndex(e => new { e.FChainId, e.FUserId }).IsUnique().HasDatabaseName("IX_WF链路关注_链路用户");
    }
}
