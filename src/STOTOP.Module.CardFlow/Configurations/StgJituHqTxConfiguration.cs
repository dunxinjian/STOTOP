using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgJituHqTxConfiguration : IEntityTypeConfiguration<StgJituHqTx>
{
    public void Configure(EntityTypeBuilder<StgJituHqTx> builder)
    {
        builder.ToTable("STG极兔总部交易明细");

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
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F运单编号).HasColumnName("F运单编号").HasMaxLength(200);
        builder.Property(e => e.F账户ID).HasColumnName("F账户ID").HasMaxLength(100);
        builder.Property(e => e.F业务日期).HasColumnName("F业务日期");
        builder.Property(e => e.F所属网点).HasColumnName("F所属网点").HasMaxLength(200);
        builder.Property(e => e.F网点编号).HasColumnName("F网点编号").HasMaxLength(200);
        builder.Property(e => e.F网点名称).HasColumnName("F网点名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F所属代理).HasColumnName("F所属代理").HasMaxLength(200);
        builder.Property(e => e.F交易类型).HasColumnName("F交易类型").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F转运中心).HasColumnName("F转运中心").HasMaxLength(200);
        builder.Property(e => e.F结算中心).HasColumnName("F结算中心").HasMaxLength(200);
        builder.Property(e => e.F结算对象).HasColumnName("F结算对象").HasMaxLength(200);
        builder.Property(e => e.F费用主类).HasColumnName("F费用主类").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F费用子类).HasColumnName("F费用子类").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F发生金额).HasColumnName("F发生金额").HasColumnType("money");
        builder.Property(e => e.F本次余额).HasColumnName("F本次余额").HasColumnType("money");
        builder.Property(e => e.F预付时间).HasColumnName("F预付时间");
        builder.Property(e => e.F备注).HasColumnName("F备注").HasMaxLength(500);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG极兔总部交易明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG极兔总部交易明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");
    }
}
