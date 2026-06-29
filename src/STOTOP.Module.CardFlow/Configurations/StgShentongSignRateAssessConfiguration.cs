using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongSignRateAssessConfiguration : IEntityTypeConfiguration<StgShentongSignRateAssess>
{
    public void Configure(EntityTypeBuilder<StgShentongSignRateAssess> builder)
    {
        builder.ToTable("STG申通_签收率考核汇总");

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

        // 业务字段（仅 6 列：退化表头一期只抽全局唯一名；分时段明细未逐列建模）
        builder.Property(e => e.F日期).HasColumnName("F日期").HasMaxLength(200);
        builder.Property(e => e.F网点编号).HasColumnName("F网点编号").HasMaxLength(200);
        builder.Property(e => e.F网点名称).HasColumnName("F网点名称").HasMaxLength(200);
        builder.Property(e => e.F所属省区).HasColumnName("F所属省区").HasMaxLength(200);
        builder.Property(e => e.F应签量).HasColumnName("F应签量").HasMaxLength(200);
        builder.Property(e => e.F总金额).HasColumnName("F总金额").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_签收率考核汇总_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_签收率考核汇总_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 签收率考核 1 行/网点/日期，按「网点编号 + 日期」去重；2 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F网点编号, e.F日期, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F网点编号] IS NOT NULL AND [F网点编号] != ''")
            .HasDatabaseName("UX_STG申通_签收率考核汇总_网点日期_未撤销");
    }
}
