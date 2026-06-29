using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongSignSubstandardConfiguration : IEntityTypeConfiguration<StgShentongSignSubstandard>
{
    public void Configure(EntityTypeBuilder<StgShentongSignSubstandard> builder)
    {
        builder.ToTable("STG申通_签收未达标明细");

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

        // 业务字段（15 列，全部可空字符串）
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F应签网点).HasColumnName("F应签网点").HasMaxLength(200);
        builder.Property(e => e.F应签网点所属独立网点).HasColumnName("F应签网点所属独立网点").HasMaxLength(200);
        builder.Property(e => e.F应签日期).HasColumnName("F应签日期").HasMaxLength(200);
        builder.Property(e => e.F签收时间).HasColumnName("F签收时间").HasMaxLength(200);
        builder.Property(e => e.F业务员).HasColumnName("F业务员").HasMaxLength(200);
        builder.Property(e => e.F当日签收标识).HasColumnName("F当日签收标识").HasMaxLength(200);
        builder.Property(e => e.F派件网点).HasColumnName("F派件网点").HasMaxLength(200);
        builder.Property(e => e.F签收网点).HasColumnName("F签收网点").HasMaxLength(200);
        builder.Property(e => e.F签收网点所属独立网点).HasColumnName("F签收网点所属独立网点").HasMaxLength(200);
        builder.Property(e => e.F是否已签收).HasColumnName("F是否已签收").HasMaxLength(200);
        builder.Property(e => e.F是否未签收有问题件).HasColumnName("F是否未签收有问题件").HasMaxLength(200);
        builder.Property(e => e.F是否曾经退回件).HasColumnName("F是否曾经退回件").HasMaxLength(200);
        builder.Property(e => e.F退回扫描时间).HasColumnName("F退回扫描时间").HasMaxLength(200);
        builder.Property(e => e.F是否曾经问题件).HasColumnName("F是否曾经问题件").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_签收未达标明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_签收未达标明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 签收未达标明细按「运单号 + 应签日期」去重；2 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.F应签日期, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_签收未达标明细_运单应签日期_未撤销");
    }
}
