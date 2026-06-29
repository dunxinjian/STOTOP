using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpBillingCostBreakdownHistoryConfiguration : IEntityTypeConfiguration<ExpBillingCostBreakdownHistory>
{
    public void Configure(EntityTypeBuilder<ExpBillingCostBreakdownHistory> builder)
    {
        builder.ToTable("EXP出港运单_计费结果_成本明细_历史");

        builder.Property(e => e.FID).HasColumnName("FID").ValueGeneratedNever();
        builder.Property(e => e.FBillingResultId).HasColumnName("F计费结果ID");
        builder.Property(e => e.FCostItemId).HasColumnName("F成本项目ID");
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasPrecision(12, 2);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FArchivedAt).HasColumnName("F归档时间");

        builder.HasIndex(e => e.FArchivedAt)
            .HasDatabaseName("IX_EXP计费结果_成本明细_历史_归档时间");
    }
}
