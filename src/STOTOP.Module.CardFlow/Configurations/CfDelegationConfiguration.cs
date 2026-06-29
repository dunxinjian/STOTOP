using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfDelegationConfiguration : IEntityTypeConfiguration<CfDelegation>
{
    public void Configure(EntityTypeBuilder<CfDelegation> builder)
    {
        builder.ToTable("CF代审批委托");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FDelegatorId).HasColumnName("F委托人ID");
        builder.Property(e => e.FDelegatorName).HasColumnName("F委托人姓名").HasMaxLength(100);
        builder.Property(e => e.FTrusteeId).HasColumnName("F受托人ID");
        builder.Property(e => e.FTrusteeName).HasColumnName("F受托人姓名").HasMaxLength(100);
        builder.Property(e => e.FStartTime).HasColumnName("F生效时间");
        builder.Property(e => e.FEndTime).HasColumnName("F失效时间");
        builder.Property(e => e.FApplicableFlowsJson).HasColumnName("F适用流程");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(30);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");

        builder.HasIndex(e => new { e.FDelegatorId, e.FStatus }).HasDatabaseName("IX_CF代审批委托_委托人");
        builder.HasIndex(e => new { e.FTrusteeId, e.FStatus }).HasDatabaseName("IX_CF代审批委托_受托人");
    }
}
