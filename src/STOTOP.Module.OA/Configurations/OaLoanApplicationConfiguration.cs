using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.OA.Entities;

namespace STOTOP.Module.OA.Configurations;

public class OaLoanApplicationConfiguration : IEntityTypeConfiguration<OaLoanApplication>
{
    public void Configure(EntityTypeBuilder<OaLoanApplication> builder)
    {
        builder.ToTable("OA借款申请单");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FDocNumber).HasColumnName("F单据编号").HasMaxLength(30);
        builder.Property(e => e.FApplicantId).HasColumnName("F申请人PID");
        builder.Property(e => e.FDeptId).HasColumnName("F部门ID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FLoanAmount).HasColumnName("F借款金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FLoanReason).HasColumnName("F借款事由").HasMaxLength(500);
        builder.Property(e => e.FExpectedReturnDate).HasColumnName("F预计归还日期");
        builder.Property(e => e.FPaymentMethod).HasColumnName("F收款方式").HasMaxLength(20);
        builder.Property(e => e.FPayeeName).HasColumnName("F收款人名称").HasMaxLength(200);
        builder.Property(e => e.FPayeeAccount).HasColumnName("F收款人账号").HasMaxLength(50);
        builder.Property(e => e.FPayeeBank).HasColumnName("F收款人开户行").HasMaxLength(200);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FDocStatus).HasColumnName("F单据状态");
        builder.Property(e => e.FReimburseOffsetAmount).HasColumnName("F已报销冲抵金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FRepaidAmount).HasColumnName("F已还款金额").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FOutstandingBalance)
            .HasColumnName("F未核销余额")
            .HasColumnType("decimal(20,2)")
            .HasComputedColumnSql("(F借款金额 - F已报销冲抵金额 - F已还款金额)", stored: true);
        builder.Property(e => e.FVoucherId).HasColumnName("F凭证ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FModifiedTime).HasColumnName("F修改时间");

        builder.HasIndex(e => e.FDocNumber).IsUnique().HasDatabaseName("IX_OA借款申请单_编号");
        builder.HasIndex(e => new { e.FOrgId, e.FDocStatus }).HasDatabaseName("IX_OA借款申请单_组织状态");
    }
}
