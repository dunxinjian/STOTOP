using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongPickupAssessConfiguration : IEntityTypeConfiguration<StgShentongPickupAssess>
{
    public void Configure(EntityTypeBuilder<StgShentongPickupAssess> builder)
    {
        builder.ToTable("STG申通_揽收考核汇总");

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

        // 业务字段（19 列；揽收承包区编码 列原文末尾带空格，dbColumn 已 trim）
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(200);
        builder.Property(e => e.F电商平台).HasColumnName("F电商平台").HasMaxLength(200);
        builder.Property(e => e.F频次).HasColumnName("F频次").HasMaxLength(200);
        builder.Property(e => e.F时效类型).HasColumnName("F时效类型").HasMaxLength(200);
        builder.Property(e => e.F揽收大区).HasColumnName("F揽收大区").HasMaxLength(200);
        builder.Property(e => e.F揽收省区).HasColumnName("F揽收省区").HasMaxLength(200);
        builder.Property(e => e.F揽收省份).HasColumnName("F揽收省份").HasMaxLength(200);
        builder.Property(e => e.F揽收所属网点).HasColumnName("F揽收所属网点").HasMaxLength(200);
        builder.Property(e => e.F揽收所属网点编码).HasColumnName("F揽收所属网点编码").HasMaxLength(200);
        builder.Property(e => e.F揽收承包区).HasColumnName("F揽收承包区").HasMaxLength(200);
        builder.Property(e => e.F揽收承包区编码).HasColumnName("F揽收承包区编码").HasMaxLength(200);
        builder.Property(e => e.F订单总量).HasColumnName("F订单总量").HasMaxLength(200);
        builder.Property(e => e.F及时揽收量).HasColumnName("F及时揽收量").HasMaxLength(200);
        builder.Property(e => e.F及时揽收率).HasColumnName("F及时揽收率").HasMaxLength(200);
        builder.Property(e => e.F未及时揽收量).HasColumnName("F未及时揽收量").HasMaxLength(200);
        builder.Property(e => e.F未及时揽收率).HasColumnName("F未及时揽收率").HasMaxLength(200);
        builder.Property(e => e.F揽收平均用时).HasColumnName("F揽收平均用时").HasMaxLength(200);
        builder.Property(e => e.F先揽后下单量).HasColumnName("F先揽后下单量").HasMaxLength(200);
        builder.Property(e => e.F超15天未揽收量).HasColumnName("F超15天未揽收量").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_揽收考核汇总_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_揽收考核汇总_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 揽收考核汇总 1 行/网点/日期，按「揽收所属网点编码 + 统计日期」去重。
        builder.HasIndex(e => new { e.F揽收所属网点编码, e.F统计日期, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F揽收所属网点编码] IS NOT NULL AND [F揽收所属网点编码] != ''")
            .HasDatabaseName("UX_STG申通_揽收考核汇总_网点日期_未撤销");
    }
}
