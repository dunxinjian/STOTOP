using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongOutboundConfiguration : IEntityTypeConfiguration<StgShentongOutbound>
{
    public void Configure(EntityTypeBuilder<StgShentongOutbound> builder)
    {
        builder.ToTable("STG申通出港运单数据");

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
        builder.Property(e => e.F所属网点).HasColumnName("F所属网点").HasMaxLength(200);
        builder.Property(e => e.F面单网点).HasColumnName("F面单网点").HasMaxLength(200);
        builder.Property(e => e.F业务日期).HasColumnName("F业务日期");
        builder.Property(e => e.F店铺账号).HasColumnName("F店铺账号").HasMaxLength(200);
        builder.Property(e => e.F共享别名).HasColumnName("F共享别名").HasMaxLength(200);
        builder.Property(e => e.F订单重量).HasColumnName("F订单重量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F结算对象).HasColumnName("F结算对象").HasMaxLength(200);
        builder.Property(e => e.F结算对象编号).HasColumnName("F结算对象编号").HasMaxLength(100);
        builder.Property(e => e.F结算类型).HasColumnName("F结算类型").HasMaxLength(100);
        builder.Property(e => e.F网点重量).HasColumnName("F网点重量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F集包重量).HasColumnName("F集包重量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F计泡重量).HasColumnName("F计泡重量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F中心重量).HasColumnName("F中心重量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F总部重量).HasColumnName("F总部重量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F结算重量).HasColumnName("F结算重量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F三方重量).HasColumnName("F三方重量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F揽收重量).HasColumnName("F揽收重量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F中转重量).HasColumnName("F中转重量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F到件重量).HasColumnName("F到件重量").HasColumnType("decimal(18,4)");
        builder.Property(e => e.F声明价值).HasColumnName("F声明价值").HasColumnType("decimal(18,2)");
        builder.Property(e => e.F目的省份).HasColumnName("F目的省份").HasMaxLength(50);
        builder.Property(e => e.F目的城市).HasColumnName("F目的城市").HasMaxLength(50);
        builder.Property(e => e.F一单到底).HasColumnName("F一单到底").HasMaxLength(20);
        builder.Property(e => e.F计算状态).HasColumnName("F计算状态");
        builder.Property(e => e.F签收网点).HasColumnName("F签收网点").HasMaxLength(200);
        builder.Property(e => e.F退回费).HasColumnName("F退回费").HasColumnType("decimal(18,2)");
        builder.Property(e => e.F操作人).HasColumnName("F操作人").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通出港运单数据_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通出港运单数据_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        builder.HasIndex(e => new { e.F运单编号, e.F所属网点, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单编号] IS NOT NULL AND [F运单编号] != ''")
            .HasDatabaseName("UX_STG申通出港运单_运单网点_未撤销");
    }
}
