using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Configurations;

public class SalaryPayrollDetailConfiguration : IEntityTypeConfiguration<SalaryPayrollDetail>
{
    public void Configure(EntityTypeBuilder<SalaryPayrollDetail> builder)
    {
        builder.ToTable("SAL工资明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F工资单ID).HasColumnName("F工资单ID");
        builder.Property(e => e.F项目类型).HasColumnName("F项目类型");
        builder.Property(e => e.F项目名称).HasColumnName("F项目名称").HasMaxLength(64).IsRequired();
        builder.Property(e => e.F金额).HasColumnName("F金额").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F来源ID).HasColumnName("F来源ID");
        builder.Property(e => e.F来源类型).HasColumnName("F来源类型").HasMaxLength(64);
        builder.Property(e => e.F备注).HasColumnName("F备注").HasMaxLength(256);

        builder.HasIndex(e => e.F工资单ID).HasDatabaseName("IX_SAL工资明细_工资单ID");
    }
}
