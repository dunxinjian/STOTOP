using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Configurations;

public class PmPointApplicationConfiguration : IEntityTypeConfiguration<PmPointApplication>
{
    public void Configure(EntityTypeBuilder<PmPointApplication> builder)
    {
        builder.ToTable("PM积分申请");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FApplicantId).HasColumnName("F申请人ID");
        builder.Property(e => e.FRuleId).HasColumnName("F规则ID");
        builder.Property(e => e.FApplicationNote).HasColumnName("F申请说明").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FAttachment).HasColumnName("F附件").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FApproverId).HasColumnName("F审批人ID");
        builder.Property(e => e.FApprovalComment).HasColumnName("F审批意见").HasMaxLength(200);
        builder.Property(e => e.FApprovalTime).HasColumnName("F审批时间");
        builder.Property(e => e.F账户类型).HasColumnName("F账户类型").HasDefaultValue(2);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.FStatus }).HasDatabaseName("IX_PM积分申请_组织_状态");
        builder.HasIndex(e => e.FApplicantId).HasDatabaseName("IX_PM积分申请_申请人");
    }
}
