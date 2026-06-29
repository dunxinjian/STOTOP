using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysMigrationLogConfiguration : IEntityTypeConfiguration<SysMigrationLog>
{
    public void Configure(EntityTypeBuilder<SysMigrationLog> builder)
    {
        builder.ToTable("SYS迁移执行日志");

        builder.HasKey(e => e.FID);
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F模块).HasColumnName("F模块").HasMaxLength(50);
        builder.Property(e => e.F版本号).HasColumnName("F版本号");
        builder.Property(e => e.F描述).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.F状态).HasColumnName("F状态").HasMaxLength(20);
        builder.Property(e => e.F错误消息).HasColumnName("F错误消息");
        builder.Property(e => e.F执行时间).HasColumnName("F执行时间").HasDefaultValueSql("getdate()");
        builder.Property(e => e.F耗时ms).HasColumnName("F耗时ms");
        builder.Property(e => e.F实例标识).HasColumnName("F实例标识").HasMaxLength(100);

        builder.HasIndex(e => new { e.F模块, e.F版本号 }).HasDatabaseName("IX_SYS迁移执行日志_模块版本");
    }
}
