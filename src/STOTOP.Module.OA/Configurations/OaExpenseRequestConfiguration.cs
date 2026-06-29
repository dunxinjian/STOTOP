using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.OA.Entities;

namespace STOTOP.Module.OA.Configurations;

public class OaExpenseRequestConfiguration : IEntityTypeConfiguration<OaExpenseRequest>
{
    public void Configure(EntityTypeBuilder<OaExpenseRequest> builder)
    {
        builder.ToTable("OA费用请款单");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FDocNumber).HasColumnName("F单据编号").HasMaxLength(30);
        builder.Property(e => e.FApplicantId).HasColumnName("F申请人ID");
        builder.Property(e => e.FDeptId).HasColumnName("F部门ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FReason).HasColumnName("F请款事由").HasMaxLength(500);
        builder.Property(e => e.FAmount).HasColumnName("F请款金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FExpectedPayDate).HasColumnName("F期望付款日期");
        builder.Property(e => e.FExpenseType).HasColumnName("F费用类型").HasMaxLength(50);
        builder.Property(e => e.FPayeeName).HasColumnName("F收款方名称").HasMaxLength(200);
        builder.Property(e => e.FPayeeAccount).HasColumnName("F收款方账号").HasMaxLength(50);
        builder.Property(e => e.FPayeeBank).HasColumnName("F收款方开户行").HasMaxLength(200);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FDocStatus).HasColumnName("F单据状态");
        builder.Property(e => e.FReferencedAmount).HasColumnName("F已引用金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FModifiedTime).HasColumnName("F修改时间");

        builder.HasIndex(e => e.FDocNumber).IsUnique().HasDatabaseName("IX_OA费用请款单_编号");
        builder.HasIndex(e => new { e.FOrgId, e.FDocStatus }).HasDatabaseName("IX_OA费用请款单_组织状态");
    }
}
