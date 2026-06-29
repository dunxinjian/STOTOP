using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.HR.Entities;

namespace STOTOP.Module.HR.Configurations;

public class HrEmployeePaymentAccountConfiguration : IEntityTypeConfiguration<HrEmployeePaymentAccount>
{
    public void Configure(EntityTypeBuilder<HrEmployeePaymentAccount> builder)
    {
        builder.ToTable("HR员工收款账号");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FEmployeeId).HasColumnName("F员工ID");
        builder.Property(e => e.FAccountType).HasColumnName("F账号类型").HasMaxLength(20);
        builder.Property(e => e.FAccountName).HasColumnName("F账号名称").HasMaxLength(50);
        builder.Property(e => e.FAccountNumber).HasColumnName("F账号").HasMaxLength(50);
        builder.Property(e => e.FBankName).HasColumnName("F银行名称").HasMaxLength(50);
        builder.Property(e => e.FBankBranch).HasColumnName("F支行名称").HasMaxLength(100);
        builder.Property(e => e.FIsDefault).HasColumnName("F是否默认").HasDefaultValue(0);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");
    }
}
