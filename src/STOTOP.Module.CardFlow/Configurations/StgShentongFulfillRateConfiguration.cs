using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongFulfillRateConfiguration : IEntityTypeConfiguration<StgShentongFulfillRate>
{
    public void Configure(EntityTypeBuilder<StgShentongFulfillRate> builder)
    {
        builder.ToTable("STG申通_履约率明细");

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
        builder.Property(e => e.F日期).HasColumnName("F日期").HasMaxLength(200);
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F收件人).HasColumnName("F收件人").HasMaxLength(200);
        builder.Property(e => e.F收件地址).HasColumnName("F收件地址").HasMaxLength(200);
        builder.Property(e => e.F履约要求).HasColumnName("F履约要求").HasMaxLength(200);
        builder.Property(e => e.F履约状态).HasColumnName("F履约状态").HasMaxLength(200);
        builder.Property(e => e.F是否虚假上门).HasColumnName("F是否虚假上门").HasMaxLength(200);
        builder.Property(e => e.F小件员名称).HasColumnName("F小件员名称").HasMaxLength(200);
        builder.Property(e => e.F小件员工号).HasColumnName("F小件员工号").HasMaxLength(200);
        builder.Property(e => e.F签收时间).HasColumnName("F签收时间").HasMaxLength(200);
        builder.Property(e => e.F首次签收类型).HasColumnName("F首次签收类型").HasMaxLength(200);
        builder.Property(e => e.F签收人).HasColumnName("F签收人").HasMaxLength(200);
        builder.Property(e => e.F服务要求).HasColumnName("F服务要求").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_履约率明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_履约率明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 履约率明细每运单一行，按运单号去重；1 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_履约率明细_运单号_未撤销");
    }
}
