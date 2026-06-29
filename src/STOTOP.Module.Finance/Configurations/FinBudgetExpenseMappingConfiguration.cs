using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinBudgetExpenseMappingConfiguration : IEntityTypeConfiguration<FinBudgetExpenseMapping>
{
    public void Configure(EntityTypeBuilder<FinBudgetExpenseMapping> builder)
    {
        builder.ToTable("FIN预算费用映射");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FExpenseType).HasColumnName("F费用类型").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FAccountCode).HasColumnName("F科目编码").HasMaxLength(50);
        builder.Property(e => e.FPLItemId).HasColumnName("F损益项ID");
        builder.Property(e => e.FCashCategory).HasColumnName("F资金分类").HasMaxLength(50).HasDefaultValue("expense_reimbursement");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => new { e.FAccountSetId, e.FOrgId, e.FExpenseType })
            .IsUnique()
            .HasDatabaseName("IX_FIN预算费用映射_账套组织费用类型");
    }
}
