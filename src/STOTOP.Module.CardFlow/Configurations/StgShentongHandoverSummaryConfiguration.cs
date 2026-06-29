using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongHandoverSummaryConfiguration : IEntityTypeConfiguration<StgShentongHandoverSummary>
{
    public void Configure(EntityTypeBuilder<StgShentongHandoverSummary> builder)
    {
        builder.ToTable("STG申通_交货滞留汇总");

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

        // 业务字段（30 列；括号/连字符/'&' 列 dbColumn 已去除非法字符）
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(200);
        builder.Property(e => e.F揽收网点).HasColumnName("F揽收网点").HasMaxLength(200);
        builder.Property(e => e.F揽收网点编码).HasColumnName("F揽收网点编码").HasMaxLength(200);
        builder.Property(e => e.F揽收网点所属网点).HasColumnName("F揽收网点所属网点").HasMaxLength(200);
        builder.Property(e => e.F揽收所属网点编码).HasColumnName("F揽收所属网点编码").HasMaxLength(200);
        builder.Property(e => e.F揽收网点省区).HasColumnName("F揽收网点省区").HasMaxLength(200);
        builder.Property(e => e.F揽收网点大区).HasColumnName("F揽收网点大区").HasMaxLength(200);
        builder.Property(e => e.F揽收网点省份).HasColumnName("F揽收网点省份").HasMaxLength(200);
        builder.Property(e => e.F中心名称).HasColumnName("F中心名称").HasMaxLength(200);
        builder.Property(e => e.F客户编码).HasColumnName("F客户编码").HasMaxLength(200);
        builder.Property(e => e.F客户名称).HasColumnName("F客户名称").HasMaxLength(200);
        builder.Property(e => e.F线路类型).HasColumnName("F线路类型").HasMaxLength(200);
        builder.Property(e => e.F原始揽收量).HasColumnName("F原始揽收量").HasMaxLength(200);
        builder.Property(e => e.F总揽收量).HasColumnName("F总揽收量").HasMaxLength(200);
        builder.Property(e => e.F交货平均用时h).HasColumnName("F交货平均用时h").HasMaxLength(200);
        builder.Property(e => e.F交货及时量).HasColumnName("F交货及时量").HasMaxLength(200);
        builder.Property(e => e.F交货延误量).HasColumnName("F交货延误量").HasMaxLength(200);
        builder.Property(e => e.F总滞留量).HasColumnName("F总滞留量").HasMaxLength(200);
        builder.Property(e => e.F未交货量).HasColumnName("F未交货量").HasMaxLength(200);
        builder.Property(e => e.F滞留率).HasColumnName("F滞留率").HasMaxLength(200);
        builder.Property(e => e.F目标值).HasColumnName("F目标值").HasMaxLength(200);
        builder.Property(e => e.F揽收超48h总量).HasColumnName("F揽收超48h总量").HasMaxLength(200);
        builder.Property(e => e.F揽收超48h已交货量).HasColumnName("F揽收超48h已交货量").HasMaxLength(200);
        builder.Property(e => e.F揽收超48h未交货量).HasColumnName("F揽收超48h未交货量").HasMaxLength(200);
        builder.Property(e => e.F考核滞留揽收超48h量).HasColumnName("F考核滞留揽收超48h量").HasMaxLength(200);
        builder.Property(e => e.F揽收超48小时预估考核日).HasColumnName("F揽收超48小时预估考核日").HasMaxLength(200);
        builder.Property(e => e.F滞留预估考核日).HasColumnName("F滞留预估考核日").HasMaxLength(200);
        builder.Property(e => e.F考核滞留量).HasColumnName("F考核滞留量").HasMaxLength(200);
        builder.Property(e => e.F白名单标识).HasColumnName("F白名单标识").HasMaxLength(200);
        builder.Property(e => e.F分频次标识).HasColumnName("F分频次标识").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_交货滞留汇总_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_交货滞留汇总_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 交货滞留汇总 1 行/网点/日期，按「揽收所属网点编码 + 统计日期」去重。
        builder.HasIndex(e => new { e.F揽收所属网点编码, e.F统计日期, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F揽收所属网点编码] IS NOT NULL AND [F揽收所属网点编码] != ''")
            .HasDatabaseName("UX_STG申通_交货滞留汇总_网点日期_未撤销");
    }
}
