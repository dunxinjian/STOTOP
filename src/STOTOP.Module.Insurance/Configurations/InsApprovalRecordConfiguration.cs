using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Insurance.Entities;

namespace STOTOP.Module.Insurance.Configurations;

public class InsApprovalRecordConfiguration : IEntityTypeConfiguration<InsApprovalRecord>
{
    public void Configure(EntityTypeBuilder<InsApprovalRecord> builder)
    {
        builder.ToTable("INS理赔审批记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FSettlementId).HasColumnName("F理赔ID").IsRequired();
        builder.Property(e => e.FStepConfigId).HasColumnName("F环节配置ID").IsRequired();
        builder.Property(e => e.FStepOrder).HasColumnName("F环节序号").IsRequired();
        builder.Property(e => e.FStepName).HasColumnName("F环节名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FApproverId).HasColumnName("F审批人ID").IsRequired();
        builder.Property(e => e.FApproverName).HasColumnName("F审批人姓名").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FApprovalAction).HasColumnName("F审批动作").IsRequired();
        builder.Property(e => e.FApprovalComment).HasColumnName("F审批意见").HasMaxLength(1000);
        builder.Property(e => e.FTransferTargetId).HasColumnName("F转办目标人ID");
        builder.Property(e => e.FTransferTargetName).HasColumnName("F转办目标人姓名").HasMaxLength(100);
        builder.Property(e => e.FApprovalTime).HasColumnName("F审批时间").IsRequired();
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");

        builder.HasIndex(e => e.FSettlementId)
            .HasDatabaseName("IX_INS理赔审批记录_理赔ID");

        builder.HasOne(e => e.StepConfig)
            .WithMany()
            .HasForeignKey(e => e.FStepConfigId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
