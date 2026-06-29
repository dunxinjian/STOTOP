using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Configurations;

public class SalaryPayrollConfiguration : IEntityTypeConfiguration<SalaryPayroll>
{
    public void Configure(EntityTypeBuilder<SalaryPayroll> builder)
    {
        builder.ToTable("SAL月度工资单");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F期间).HasColumnName("F期间").HasMaxLength(6).IsRequired();
        builder.Property(e => e.F基本工资).HasColumnName("F基本工资").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.FKSF浮动).HasColumnName("FKSF浮动").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.FPPV奖金).HasColumnName("FPPV奖金").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.FB分兑换).HasColumnName("FB分兑换").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F考勤扣减).HasColumnName("F考勤扣减").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F社保个人).HasColumnName("F社保个人").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F公积金个人).HasColumnName("F公积金个人").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F个税).HasColumnName("F个税").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F应发合计).HasColumnName("F应发合计").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F实发合计).HasColumnName("F实发合计").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F状态).HasColumnName("F状态");
        builder.Property(e => e.F审核人ID).HasColumnName("F审核人ID");
        builder.Property(e => e.F审核时间).HasColumnName("F审核时间");
        builder.Property(e => e.F发放时间).HasColumnName("F发放时间");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F员工ID, e.F期间 }).IsUnique().HasDatabaseName("UQ_SAL月度工资单_员工_期间");
    }
}
