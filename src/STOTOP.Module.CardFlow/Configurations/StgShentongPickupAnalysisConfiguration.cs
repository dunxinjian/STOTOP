using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongPickupAnalysisConfiguration : IEntityTypeConfiguration<StgShentongPickupAnalysis>
{
    public void Configure(EntityTypeBuilder<StgShentongPickupAnalysis> builder)
    {
        builder.ToTable("STG申通_揽收分析明细");

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

        // 业务字段（24 列，全部可空字符串）
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(100);
        builder.Property(e => e.F电商平台).HasColumnName("F电商平台").HasMaxLength(100);
        builder.Property(e => e.F运单编号).HasColumnName("F运单编号").HasMaxLength(200);
        builder.Property(e => e.F订单编号).HasColumnName("F订单编号").HasMaxLength(200);
        builder.Property(e => e.F时效类型).HasColumnName("F时效类型").HasMaxLength(100);
        builder.Property(e => e.F频次).HasColumnName("F频次").HasMaxLength(50);
        builder.Property(e => e.F订单时间).HasColumnName("F订单时间").HasMaxLength(100);
        builder.Property(e => e.F揽收时间).HasColumnName("F揽收时间").HasMaxLength(100);
        builder.Property(e => e.F揽收截止时间).HasColumnName("F揽收截止时间").HasMaxLength(100);
        builder.Property(e => e.F订单揽收用时h).HasColumnName("F订单揽收用时h").HasMaxLength(50);
        builder.Property(e => e.F揽收标识).HasColumnName("F揽收标识").HasMaxLength(50);
        builder.Property(e => e.F揽收及时标识).HasColumnName("F揽收及时标识").HasMaxLength(50);
        builder.Property(e => e.F商家名称).HasColumnName("F商家名称").HasMaxLength(200);
        builder.Property(e => e.F订单网点).HasColumnName("F订单网点").HasMaxLength(200);
        builder.Property(e => e.F订单所属网点).HasColumnName("F订单所属网点").HasMaxLength(200);
        builder.Property(e => e.F揽收网点).HasColumnName("F揽收网点").HasMaxLength(200);
        builder.Property(e => e.F揽收所属网点).HasColumnName("F揽收所属网点").HasMaxLength(200);
        builder.Property(e => e.F收件员).HasColumnName("F收件员").HasMaxLength(200);
        builder.Property(e => e.F订单始发城市).HasColumnName("F订单始发城市").HasMaxLength(100);
        builder.Property(e => e.F订单目的城市).HasColumnName("F订单目的城市").HasMaxLength(100);
        builder.Property(e => e.F仓类型).HasColumnName("F仓类型").HasMaxLength(100);
        builder.Property(e => e.F菜鸟仓编号).HasColumnName("F菜鸟仓编号").HasMaxLength(100);
        builder.Property(e => e.F菜鸟仓名称).HasColumnName("F菜鸟仓名称").HasMaxLength(200);
        builder.Property(e => e.F揽收超15天标识).HasColumnName("F揽收超15天标识").HasMaxLength(50);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_揽收分析明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_揽收分析明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 揽收分析明细每运单一行，按运单编号去重；1 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单编号, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单编号] IS NOT NULL AND [F运单编号] != ''")
            .HasDatabaseName("UX_STG申通_揽收分析明细_运单编号_未撤销");
    }
}
