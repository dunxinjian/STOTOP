using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinBudgetLineConfiguration : IEntityTypeConfiguration<FinBudgetLine>
{
    public void Configure(EntityTypeBuilder<FinBudgetLine> builder)
    {
        builder.ToTable("FIN预算明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FBudgetVersionId).HasColumnName("F预算版本ID");
        builder.Property(e => e.FPeriod).HasColumnName("F期间").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FAmoebaUnitId).HasColumnName("F阿米巴单元ID");
        builder.Property(e => e.FAccountId).HasColumnName("F科目ID");
        builder.Property(e => e.FAccountCode).HasColumnName("F科目编码").HasMaxLength(50);
        builder.Property(e => e.FPLItemId).HasColumnName("F损益项ID");
        builder.Property(e => e.FDimensionJson).HasColumnName("F维度Json").HasColumnType("nvarchar(max)");
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasColumnType("decimal(18,4)");
        builder.Property(e => e.FQuantity).HasColumnName("F数量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.FUnitPrice).HasColumnName("F单价").HasColumnType("decimal(18,4)");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FBudgetVersionId, e.FPeriod, e.FOrgId, e.FPLItemId })
            .HasDatabaseName("IX_FIN预算明细_版本期间组织损益项");
        builder.HasIndex(e => new { e.FBudgetVersionId, e.FPeriod, e.FOrgId, e.FAccountCode })
            .HasDatabaseName("IX_FIN预算明细_版本期间组织科目");
    }
}
