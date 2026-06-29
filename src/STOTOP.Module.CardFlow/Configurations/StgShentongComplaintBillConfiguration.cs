using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongComplaintBillConfiguration : IEntityTypeConfiguration<StgShentongComplaintBill>
{
    public void Configure(EntityTypeBuilder<StgShentongComplaintBill> builder)
    {
        builder.ToTable("STG申通_投诉账单明细");

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

        // 业务字段（23 列，全部可空字符串；均为 row2 中全局唯一列名）
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F账单一级类型).HasColumnName("F账单一级类型").HasMaxLength(200);
        builder.Property(e => e.F账单二级类型).HasColumnName("F账单二级类型").HasMaxLength(200);
        builder.Property(e => e.F金额).HasColumnName("F金额").HasMaxLength(200);
        builder.Property(e => e.F理赔来源).HasColumnName("F理赔来源").HasMaxLength(200);
        builder.Property(e => e.F账单生成时间).HasColumnName("F账单生成时间").HasMaxLength(200);
        builder.Property(e => e.F申诉完结时间).HasColumnName("F申诉完结时间").HasMaxLength(200);
        builder.Property(e => e.F理赔类型).HasColumnName("F理赔类型").HasMaxLength(200);
        builder.Property(e => e.F处理结果).HasColumnName("F处理结果").HasMaxLength(200);
        builder.Property(e => e.F投诉网点).HasColumnName("F投诉网点").HasMaxLength(200);
        builder.Property(e => e.F被投诉方1).HasColumnName("F被投诉方1").HasMaxLength(200);
        builder.Property(e => e.F完结方式).HasColumnName("F完结方式").HasMaxLength(200);
        builder.Property(e => e.F投诉时间).HasColumnName("F投诉时间").HasMaxLength(200);
        builder.Property(e => e.F补录时间).HasColumnName("F补录时间").HasMaxLength(200);
        builder.Property(e => e.F内件品名).HasColumnName("F内件品名").HasMaxLength(200);
        builder.Property(e => e.F内件实际价值).HasColumnName("F内件实际价值").HasMaxLength(200);
        builder.Property(e => e.F调查经过).HasColumnName("F调查经过").HasMaxLength(200);
        builder.Property(e => e.F处理人).HasColumnName("F处理人").HasMaxLength(200);
        builder.Property(e => e.F总部主管审核人姓名).HasColumnName("F总部主管审核人姓名").HasMaxLength(200);
        builder.Property(e => e.F受款方网点编号).HasColumnName("F受款方网点编号").HasMaxLength(200);
        builder.Property(e => e.F受款方网点名称).HasColumnName("F受款方网点名称").HasMaxLength(200);
        builder.Property(e => e.F受款方应受款金额).HasColumnName("F受款方应受款金额").HasMaxLength(200);
        builder.Property(e => e.F受款方协商受款金额).HasColumnName("F受款方协商受款金额").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_投诉账单明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_投诉账单明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 投诉账单明细按「运单号 + 账单生成时间」去重；2 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.F账单生成时间, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_投诉账单明细_运单账单时间_未撤销");
    }
}
