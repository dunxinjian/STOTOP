using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongHqTxConfiguration : IEntityTypeConfiguration<StgShentongHqTx>
{
    public void Configure(EntityTypeBuilder<StgShentongHqTx> builder)
    {
        builder.ToTable("STG申通总部交易明细");

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

        // 业务字段
        builder.Property(e => e.F运单编号).HasColumnName("F运单编号").HasMaxLength(200);
        builder.Property(e => e.F业务日期).HasColumnName("F业务日期").IsRequired();
        builder.Property(e => e.F记账日期).HasColumnName("F记账日期");
        builder.Property(e => e.F业务摘要).HasColumnName("F业务摘要").HasMaxLength(200);
        builder.Property(e => e.F网点编号).HasColumnName("F网点编号").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F网点名称).HasColumnName("F网点名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F费用类型).HasColumnName("F费用类型").HasMaxLength(200);
        builder.Property(e => e.F费用名称).HasColumnName("F费用名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F发生额收入).HasColumnName("F发生额收入").HasColumnType("money");
        builder.Property(e => e.F发生额支出).HasColumnName("F发生额支出").HasColumnType("money");
        builder.Property(e => e.F余额).HasColumnName("F余额").HasColumnType("money");
        builder.Property(e => e.F账单类型).HasColumnName("F账单类型").HasMaxLength(50);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);
        builder.Property(e => e.F备注).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.F结算方式).HasColumnName("F结算方式").HasMaxLength(200);
        builder.Property(e => e.F结算周期).HasColumnName("F结算周期").HasMaxLength(200);
        builder.Property(e => e.F操作人).HasColumnName("F操作人").HasMaxLength(200);
        builder.Property(e => e.F科目编码).HasColumnName("F科目编码").HasMaxLength(100);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通总部交易明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通总部交易明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");
    }
}
