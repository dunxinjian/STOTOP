using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.OA.Entities;

namespace STOTOP.Module.OA.Configurations;

public class OaExpenseReimbursementConfiguration : IEntityTypeConfiguration<OaExpenseReimbursement>
{
    public void Configure(EntityTypeBuilder<OaExpenseReimbursement> builder)
    {
        builder.ToTable("OA费用报销单");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FDocNumber).HasColumnName("F单据编号").HasMaxLength(30);
        builder.Property(e => e.FApplicantId).HasColumnName("F申请人ID");
        builder.Property(e => e.FDeptId).HasColumnName("F部门ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FReason).HasColumnName("F报销事由").HasMaxLength(500);
        builder.Property(e => e.FTotalAmount).HasColumnName("F报销总金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FRequestRefId).HasColumnName("F引用请款单ID");
        builder.Property(e => e.FLoanRefId).HasColumnName("F引用借款单ID");
        builder.Property(e => e.FPaymentMethod).HasColumnName("F收款方式").HasMaxLength(20);
        builder.Property(e => e.FPayeeName).HasColumnName("F收款人名称").HasMaxLength(200);
        builder.Property(e => e.FPayeeAccount).HasColumnName("F收款人账号").HasMaxLength(50);
        builder.Property(e => e.FPayeeBank).HasColumnName("F收款人开户行").HasMaxLength(200);
        builder.Property(e => e.FAttachmentCount).HasColumnName("F附件数");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FDocStatus).HasColumnName("F单据状态");
        builder.Property(e => e.FVoucherId).HasColumnName("F凭证ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FModifiedTime).HasColumnName("F修改时间");

        builder.HasIndex(e => e.FDocNumber).IsUnique().HasDatabaseName("IX_OA费用报销单_编号");
        builder.HasIndex(e => new { e.FOrgId, e.FDocStatus }).HasDatabaseName("IX_OA费用报销单_组织状态");

        builder.HasMany(e => e.Details)
            .WithOne()
            .HasForeignKey(d => d.FReimbursementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
