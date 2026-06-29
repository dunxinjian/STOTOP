using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinBudgetVersionConfiguration : IEntityTypeConfiguration<FinBudgetVersion>
{
    public void Configure(EntityTypeBuilder<FinBudgetVersion> builder)
    {
        builder.ToTable("FIN预算版本");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FScenarioType).HasColumnName("F场景类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FYear).HasColumnName("F年度");
        builder.Property(e => e.FBaseVersionId).HasColumnName("F来源版本ID");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30).HasDefaultValue("draft");
        builder.Property(e => e.FOwnerOrgId).HasColumnName("F归属组织ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FCreatedBy).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FApprovedBy).HasColumnName("F审批人").HasMaxLength(50);
        builder.Property(e => e.FApprovedTime).HasColumnName("F审批时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FAccountSetId, e.FYear, e.FScenarioType, e.FStatus })
            .HasDatabaseName("IX_FIN预算版本_账套年度场景状态");
    }
}
