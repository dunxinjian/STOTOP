using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Configurations;

/// <summary>
/// PM积分清算记录 表配置
/// 唯一约束：(F组织ID, F员工ID, F账户类型, F清算期间, F清算类型) 保证清算 Job 重跑幂等
/// </summary>
public class PmPointResetRecordConfiguration : IEntityTypeConfiguration<PmPointResetRecord>
{
    public void Configure(EntityTypeBuilder<PmPointResetRecord> builder)
    {
        builder.ToTable("PM积分清算记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F账户类型).HasColumnName("F账户类型").HasDefaultValue(2);
        builder.Property(e => e.F清算期间).HasColumnName("F清算期间").HasMaxLength(16).IsRequired();
        builder.Property(e => e.F清算类型).HasColumnName("F清算类型");
        builder.Property(e => e.F清算策略).HasColumnName("F清算策略").HasDefaultValue(0);
        builder.Property(e => e.F清算前余额).HasColumnName("F清算前余额");
        builder.Property(e => e.F清算后余额).HasColumnName("F清算后余额");
        builder.Property(e => e.F转换比例).HasColumnName("F转换比例").HasColumnType("decimal(18,4)").HasDefaultValue(1.0m);
        builder.Property(e => e.F兑换福利券值).HasColumnName("F兑换福利券值").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F关联兑换记录ID).HasColumnName("F关联兑换记录ID");
        builder.Property(e => e.F快照日期).HasColumnName("F快照日期");
        builder.Property(e => e.F执行时间).HasColumnName("F执行时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.F备注).HasColumnName("F备注").HasMaxLength(500);

        builder.HasIndex(e => new { e.FOrgId, e.F员工ID, e.F账户类型, e.F清算期间, e.F清算类型 })
            .IsUnique()
            .HasDatabaseName("UQ_PM积分清算记录_组织_员工_账户类型_期间_类型");

        builder.HasIndex(e => new { e.FOrgId, e.F清算期间, e.F清算类型 })
            .HasDatabaseName("IX_PM积分清算记录_组织_期间_类型");
    }
}
