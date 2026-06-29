using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpBillingCostBreakdownConfiguration : IEntityTypeConfiguration<ExpBillingCostBreakdown>
{
    public void Configure(EntityTypeBuilder<ExpBillingCostBreakdown> builder)
    {
        builder.ToTable("EXP出港运单_计费结果_成本明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBillingResultId).HasColumnName("F计费结果ID");
        builder.Property(e => e.FCostItemId).HasColumnName("F成本项目ID");
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasPrecision(12, 2);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");

        builder.HasIndex(e => e.FBillingResultId)
            .HasDatabaseName("IX_EXP计费结果_成本明细_F计费结果ID");
    }
}
