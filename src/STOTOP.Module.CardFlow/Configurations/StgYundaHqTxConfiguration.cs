using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgYundaHqTxConfiguration : IEntityTypeConfiguration<StgYundaHqTx>
{
    public void Configure(EntityTypeBuilder<StgYundaHqTx> builder)
    {
        builder.ToTable("STG韵达总部交易明细");

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
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);
        builder.Property(e => e.F运单编号).HasColumnName("F运单编号").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F业务日期).HasColumnName("F业务日期").IsRequired();
        builder.Property(e => e.F网点业务类型).HasColumnName("F网点业务类型").HasMaxLength(100);
        builder.Property(e => e.F所属业务类型).HasColumnName("F所属业务类型").HasMaxLength(100);
        builder.Property(e => e.F交易类型名称).HasColumnName("F交易类型名称").HasMaxLength(200);
        builder.Property(e => e.F交易来源).HasColumnName("F交易来源").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F交易月份).HasColumnName("F交易月份").HasMaxLength(20).IsRequired();
        builder.Property(e => e.F到账时间).HasColumnName("F到账时间").HasMaxLength(50);
        builder.Property(e => e.F收费公司).HasColumnName("F收费公司").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F收费公司编码).HasColumnName("F收费公司编码").HasMaxLength(100).IsRequired();
        builder.Property(e => e.F费用大类).HasColumnName("F费用大类").HasMaxLength(200);
        builder.Property(e => e.F收费项目).HasColumnName("F收费项目").HasMaxLength(200);
        builder.Property(e => e.F收费编码).HasColumnName("F收费编码").HasMaxLength(100);
        builder.Property(e => e.F三级收费科目).HasColumnName("F三级收费科目").HasMaxLength(200);
        builder.Property(e => e.F三级科目编码).HasColumnName("F三级科目编码").HasMaxLength(100);
        builder.Property(e => e.F期初金额).HasColumnName("F期初金额").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F交易金额).HasColumnName("F交易金额").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F期末余额).HasColumnName("F期末余额").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F备注).HasColumnName("F备注").HasMaxLength(500).IsRequired();
        builder.Property(e => e.F结算状态).HasColumnName("F结算状态").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F操作时间).HasColumnName("F操作时间").HasMaxLength(50).IsRequired();

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG韵达总部交易明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG韵达总部交易明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");
    }
}
