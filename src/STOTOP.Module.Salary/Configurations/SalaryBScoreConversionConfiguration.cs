using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Configurations;

public class SalaryBScoreConversionConfiguration : IEntityTypeConfiguration<SalaryBScoreConversion>
{
    public void Configure(EntityTypeBuilder<SalaryBScoreConversion> builder)
    {
        builder.ToTable("SALB分兑换记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F期间).HasColumnName("F期间").HasMaxLength(6).IsRequired();
        builder.Property(e => e.FB分余额).HasColumnName("FB分余额");
        builder.Property(e => e.F兑换比例).HasColumnName("F兑换比例").HasColumnType("decimal(18,4)").HasDefaultValue(0m);
        builder.Property(e => e.F兑换金额).HasColumnName("F兑换金额").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F兑换类型).HasColumnName("F兑换类型");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F员工ID, e.F期间 }).IsUnique().HasDatabaseName("UQ_SALB分兑换_组织_员工_期间");
    }
}
