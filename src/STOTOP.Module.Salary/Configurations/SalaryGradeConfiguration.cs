using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Configurations;

public class SalaryGradeConfiguration : IEntityTypeConfiguration<SalaryGrade>
{
    public void Configure(EntityTypeBuilder<SalaryGrade> builder)
    {
        builder.ToTable("SAL薪酬档位");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F档位编码).HasColumnName("F档位编码").HasMaxLength(64).IsRequired();
        builder.Property(e => e.F档位名称).HasColumnName("F档位名称").HasMaxLength(128).IsRequired();
        builder.Property(e => e.F级别).HasColumnName("F级别");
        builder.Property(e => e.F基本工资).HasColumnName("F基本工资").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F岗位津贴).HasColumnName("F岗位津贴").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F绩效基数).HasColumnName("F绩效基数").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F生效起期).HasColumnName("F生效起期");
        builder.Property(e => e.F启用状态).HasColumnName("F启用状态").HasDefaultValue(true);
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.F更新时间).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F档位编码 }).IsUnique().HasDatabaseName("UQ_SAL薪酬档位_组织_编码");
    }
}
