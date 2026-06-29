using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfStageAssigneeConfiguration : IEntityTypeConfiguration<CfStageAssignee>
{
    public void Configure(EntityTypeBuilder<CfStageAssignee> builder)
    {
        builder.ToTable("CF节点处理人");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FStageInstanceId).HasColumnName("F节点实例ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FUserName).HasColumnName("F用户姓名").HasMaxLength(100);
        builder.Property(e => e.FRoleCode).HasColumnName("F角色编码").HasMaxLength(50);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").HasDefaultValue(0);
        builder.Property(e => e.FAssignedTime).HasColumnName("F分配时间");
        builder.Property(e => e.FCompletedTime).HasColumnName("F完成时间");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30);
        builder.Property(e => e.FOpinion).HasColumnName("F意见").HasMaxLength(500);
        builder.Property(e => e.FRowVersion).HasColumnName("F乐观锁").IsRowVersion();

        builder.HasIndex(e => e.FStageInstanceId).HasDatabaseName("IX_CF节点处理人_实例");
    }
}
