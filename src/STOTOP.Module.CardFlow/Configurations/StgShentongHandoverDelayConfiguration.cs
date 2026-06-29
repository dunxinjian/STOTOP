using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongHandoverDelayConfiguration : IEntityTypeConfiguration<StgShentongHandoverDelay>
{
    public void Configure(EntityTypeBuilder<StgShentongHandoverDelay> builder)
    {
        builder.ToTable("STG申通_交货滞留明细");

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

        // 业务字段（34 列，全部可空字符串）
        builder.Property(e => e.F业务日期).HasColumnName("F业务日期").HasMaxLength(100);
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F电商平台).HasColumnName("F电商平台").HasMaxLength(100);
        builder.Property(e => e.F客户名称).HasColumnName("F客户名称").HasMaxLength(200);
        builder.Property(e => e.F当前交货状态).HasColumnName("F当前交货状态").HasMaxLength(100);
        builder.Property(e => e.F揽收网点).HasColumnName("F揽收网点").HasMaxLength(200);
        builder.Property(e => e.F揽收所属网点).HasColumnName("F揽收所属网点").HasMaxLength(200);
        builder.Property(e => e.F装车发件网点).HasColumnName("F装车发件网点").HasMaxLength(200);
        builder.Property(e => e.F任务号).HasColumnName("F任务号").HasMaxLength(100);
        builder.Property(e => e.F车牌号).HasColumnName("F车牌号").HasMaxLength(50);
        builder.Property(e => e.F计划下一站中心).HasColumnName("F计划下一站中心").HasMaxLength(200);
        builder.Property(e => e.F实际下一站中心).HasColumnName("F实际下一站中心").HasMaxLength(200);
        builder.Property(e => e.F装车用时).HasColumnName("F装车用时").HasMaxLength(50);
        builder.Property(e => e.F在途用时).HasColumnName("F在途用时").HasMaxLength(50);
        builder.Property(e => e.F交货用时).HasColumnName("F交货用时").HasMaxLength(50);
        builder.Property(e => e.F揽收时间).HasColumnName("F揽收时间").HasMaxLength(100);
        builder.Property(e => e.F网点装车时间).HasColumnName("F网点装车时间").HasMaxLength(100);
        builder.Property(e => e.F交货时间).HasColumnName("F交货时间").HasMaxLength(100);
        builder.Property(e => e.F交货截止时间).HasColumnName("F交货截止时间").HasMaxLength(100);
        builder.Property(e => e.F中心到件时间).HasColumnName("F中心到件时间").HasMaxLength(100);
        builder.Property(e => e.F考核标识).HasColumnName("F考核标识").HasMaxLength(50);
        builder.Property(e => e.F考核达标标识).HasColumnName("F考核达标标识").HasMaxLength(50);
        builder.Property(e => e.F错发下一站标识).HasColumnName("F错发下一站标识").HasMaxLength(50);
        builder.Property(e => e.F地区件标识).HasColumnName("F地区件标识").HasMaxLength(50);
        builder.Property(e => e.F交货滞留截止时间).HasColumnName("F交货滞留截止时间").HasMaxLength(100);
        builder.Property(e => e.F交货滞留标识).HasColumnName("F交货滞留标识").HasMaxLength(50);
        builder.Property(e => e.F线路类型).HasColumnName("F线路类型").HasMaxLength(100);
        builder.Property(e => e.F内网揽收时间).HasColumnName("F内网揽收时间").HasMaxLength(100);
        builder.Property(e => e.F外网揽收时间).HasColumnName("F外网揽收时间").HasMaxLength(100);
        builder.Property(e => e.F揽收超48h标识).HasColumnName("F揽收超48h标识").HasMaxLength(50);
        builder.Property(e => e.F揽收小件员名称).HasColumnName("F揽收小件员名称").HasMaxLength(200);
        builder.Property(e => e.F首中心操作时间).HasColumnName("F首中心操作时间").HasMaxLength(100);
        builder.Property(e => e.F考核滞留且揽收超48小时标识).HasColumnName("F考核滞留且揽收超48小时标识").HasMaxLength(50);
        builder.Property(e => e.F揽收选取类型).HasColumnName("F揽收选取类型").HasMaxLength(100);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_交货滞留明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_交货滞留明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 交货滞留明细每运单一行，按运单号去重；1 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_交货滞留明细_运单号_未撤销");
    }
}
