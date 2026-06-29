using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Configurations;

public class SalaryArchiveConfiguration : IEntityTypeConfiguration<SalaryArchive>
{
    public void Configure(EntityTypeBuilder<SalaryArchive> builder)
    {
        builder.ToTable("SAL员工薪酬档案");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F档位ID).HasColumnName("F档位ID");
        builder.Property(e => e.F入档日期).HasColumnName("F入档日期");
        builder.Property(e => e.F基本工资).HasColumnName("F基本工资").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F岗位津贴).HasColumnName("F岗位津贴").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F社保基数).HasColumnName("F社保基数").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F公积金基数).HasColumnName("F公积金基数").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F个税起征额).HasColumnName("F个税起征额").HasColumnType("decimal(18,2)").HasDefaultValue(5000m);
        builder.Property(e => e.F备注).HasColumnName("F备注").HasMaxLength(512);
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.F更新时间).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F员工ID }).IsUnique().HasDatabaseName("UQ_SAL员工薪酬档案_组织_员工");
    }
}
