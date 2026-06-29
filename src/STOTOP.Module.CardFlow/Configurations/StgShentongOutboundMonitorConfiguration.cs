using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongOutboundMonitorConfiguration : IEntityTypeConfiguration<StgShentongOutboundMonitor>
{
    public void Configure(EntityTypeBuilder<StgShentongOutboundMonitor> builder)
    {
        builder.ToTable("STG申通_未出仓监控明细");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F批次ID).HasColumnName("F批次ID");
        builder.Property(e => e.F处理状态).HasColumnName("F处理状态").HasDefaultValue(0);
        builder.Property(e => e.F错误信息).HasColumnName("F错误信息").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F关联凭证ID).HasColumnName("F关联凭证ID");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FDataScopeId).HasColumnName("FDataScopeId").HasMaxLength(64);
        builder.Property(e => e.FSourceWorkItemId).HasColumnName("FSourceWorkItemId");
        builder.Property(e => e.FIsRevoked).HasColumnName("FIsRevoked");
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId");
        builder.Property(e => e.F账套ID).HasColumnName("F账套ID");
        builder.Property(e => e.F归属网点编号).HasColumnName("F归属网点编号").HasMaxLength(50);

        // 业务字段（13 列，全部可空字符串）
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(100);
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F中转站).HasColumnName("F中转站").HasMaxLength(200);
        builder.Property(e => e.F应签所属网点).HasColumnName("F应签所属网点").HasMaxLength(200);
        builder.Property(e => e.F应签所属网点编码).HasColumnName("F应签所属网点编码").HasMaxLength(100);
        builder.Property(e => e.F应签站点).HasColumnName("F应签站点").HasMaxLength(200);
        builder.Property(e => e.F应签站点编码).HasColumnName("F应签站点编码").HasMaxLength(100);
        builder.Property(e => e.F派件员).HasColumnName("F派件员").HasMaxLength(200);
        builder.Property(e => e.F三段码).HasColumnName("F三段码").HasMaxLength(100);
        builder.Property(e => e.F出仓距离).HasColumnName("F出仓距离").HasMaxLength(50);
        builder.Property(e => e.F实际出仓时间).HasColumnName("F实际出仓时间").HasMaxLength(100);
        builder.Property(e => e.F理论应出仓日期).HasColumnName("F理论应出仓日期").HasMaxLength(100);
        builder.Property(e => e.F理论应出仓时间).HasColumnName("F理论应出仓时间").HasMaxLength(100);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_未出仓监控明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_未出仓监控明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 未出仓监控明细每运单一行，按运单号去重；1 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_未出仓监控明细_运单号_未撤销");
    }
}
