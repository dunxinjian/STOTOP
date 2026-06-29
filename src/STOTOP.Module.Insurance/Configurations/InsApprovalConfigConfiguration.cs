using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Insurance.Entities;

namespace STOTOP.Module.Insurance.Configurations;

public class InsApprovalConfigConfiguration : IEntityTypeConfiguration<InsApprovalConfig>
{
    public void Configure(EntityTypeBuilder<InsApprovalConfig> builder)
    {
        builder.ToTable("INS理赔审批配置");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FStepOrder).HasColumnName("F环节序号").IsRequired();
        builder.Property(e => e.FStepName).HasColumnName("F环节名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FStepCode).HasColumnName("F环节编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FApproverType).HasColumnName("F审批人类型").IsRequired();
        builder.Property(e => e.FApproverId).HasColumnName("F审批人ID");
        builder.Property(e => e.FApproverName).HasColumnName("F审批人姓名").HasMaxLength(100);
        builder.Property(e => e.FApproverRoleCode).HasColumnName("F审批角色编码").HasMaxLength(100);
        builder.Property(e => e.FCanReject).HasColumnName("F可驳回").HasDefaultValue(true);
        builder.Property(e => e.FRejectTargetStep).HasColumnName("F驳回目标环节");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FOrgId, e.FStepOrder })
            .IsUnique()
            .HasDatabaseName("IX_INS理赔审批配置_组织_序号");
    }
}
