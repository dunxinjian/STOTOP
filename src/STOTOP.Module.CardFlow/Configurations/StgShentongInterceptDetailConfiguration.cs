using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongInterceptDetailConfiguration : IEntityTypeConfiguration<StgShentongInterceptDetail>
{
    public void Configure(EntityTypeBuilder<StgShentongInterceptDetail> builder)
    {
        builder.ToTable("STG申通_应拦截明细");

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

        // 业务字段（26 列，全部可空字符串）
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(200);
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F拦截来源).HasColumnName("F拦截来源").HasMaxLength(200);
        builder.Property(e => e.F应拦截网点).HasColumnName("F应拦截网点").HasMaxLength(200);
        builder.Property(e => e.F所属网点).HasColumnName("F所属网点").HasMaxLength(200);
        builder.Property(e => e.F拦截类型).HasColumnName("F拦截类型").HasMaxLength(200);
        builder.Property(e => e.F派件小件员).HasColumnName("F派件小件员").HasMaxLength(200);
        builder.Property(e => e.F到件时间).HasColumnName("F到件时间").HasMaxLength(200);
        builder.Property(e => e.F最新OP时间).HasColumnName("F最新OP时间").HasMaxLength(200);
        builder.Property(e => e.F最新OP节点).HasColumnName("F最新OP节点").HasMaxLength(200);
        builder.Property(e => e.F驿站名称).HasColumnName("F驿站名称").HasMaxLength(200);
        builder.Property(e => e.F退件打印时间).HasColumnName("F退件打印时间").HasMaxLength(200);
        builder.Property(e => e.F退件操作人).HasColumnName("F退件操作人").HasMaxLength(200);
        builder.Property(e => e.F最迟转出时间).HasColumnName("F最迟转出时间").HasMaxLength(200);
        builder.Property(e => e.F逆向转出时间).HasColumnName("F逆向转出时间").HasMaxLength(200);
        builder.Property(e => e.F逆向交货组织).HasColumnName("F逆向交货组织").HasMaxLength(200);
        builder.Property(e => e.F逆向转出时长).HasColumnName("F逆向转出时长").HasMaxLength(200);
        builder.Property(e => e.F逆向转出时效).HasColumnName("F逆向转出时效").HasMaxLength(200);
        builder.Property(e => e.F预计考核金额).HasColumnName("F预计考核金额").HasMaxLength(200);
        builder.Property(e => e.F拦截录入网点).HasColumnName("F拦截录入网点").HasMaxLength(200);
        builder.Property(e => e.F拦截录入时间).HasColumnName("F拦截录入时间").HasMaxLength(200);
        builder.Property(e => e.F拦截发起节点).HasColumnName("F拦截发起节点").HasMaxLength(200);
        builder.Property(e => e.F是否拦截成功).HasColumnName("F是否拦截成功").HasMaxLength(200);
        builder.Property(e => e.F是否转出).HasColumnName("F是否转出").HasMaxLength(200);
        builder.Property(e => e.F是否及时转出).HasColumnName("F是否及时转出").HasMaxLength(200);
        builder.Property(e => e.F正向签收).HasColumnName("F正向签收").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_应拦截明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_应拦截明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 应拦截明细按「运单号 + 统计日期」去重；2 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.F统计日期, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_应拦截明细_运单统计日期_未撤销");
    }
}
