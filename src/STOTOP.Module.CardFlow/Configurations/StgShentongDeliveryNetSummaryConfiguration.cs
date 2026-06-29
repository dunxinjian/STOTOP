using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongDeliveryNetSummaryConfiguration : IEntityTypeConfiguration<StgShentongDeliveryNetSummary>
{
    public void Configure(EntityTypeBuilder<StgShentongDeliveryNetSummary> builder)
    {
        builder.ToTable("STG申通_末端派送网点汇总");

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

        // 业务字段（55 列）
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(200);
        builder.Property(e => e.F中转站).HasColumnName("F中转站").HasMaxLength(200);
        builder.Property(e => e.F应签所属网点).HasColumnName("F应签所属网点").HasMaxLength(200);
        builder.Property(e => e.F应签网点).HasColumnName("F应签网点").HasMaxLength(200);
        builder.Property(e => e.F派件员).HasColumnName("F派件员").HasMaxLength(200);
        builder.Property(e => e.F四级区域).HasColumnName("F四级区域").HasMaxLength(200);
        builder.Property(e => e.F五级区域).HasColumnName("F五级区域").HasMaxLength(200);
        builder.Property(e => e.F频次类型).HasColumnName("F频次类型").HasMaxLength(200);
        builder.Property(e => e.F派次类型).HasColumnName("F派次类型").HasMaxLength(200);
        builder.Property(e => e.F预计考核金额).HasColumnName("F预计考核金额").HasMaxLength(200);
        builder.Property(e => e.F有偿派费金额).HasColumnName("F有偿派费金额").HasMaxLength(200);
        builder.Property(e => e.F预计返款金额).HasColumnName("F预计返款金额").HasMaxLength(200);
        builder.Property(e => e.F一阶段考核数量).HasColumnName("F一阶段考核数量").HasMaxLength(200);
        builder.Property(e => e.F一阶段及时签收数量).HasColumnName("F一阶段及时签收数量").HasMaxLength(200);
        builder.Property(e => e.F一阶段及时签收率).HasColumnName("F一阶段及时签收率").HasMaxLength(200);
        builder.Property(e => e.F一阶段目标值).HasColumnName("F一阶段目标值").HasMaxLength(200);
        builder.Property(e => e.F一阶段预计考核金额).HasColumnName("F一阶段预计考核金额").HasMaxLength(200);
        builder.Property(e => e.F一阶段未及时签收数量).HasColumnName("F一阶段未及时签收数量").HasMaxLength(200);
        builder.Property(e => e.F二阶段考核数量).HasColumnName("F二阶段考核数量").HasMaxLength(200);
        builder.Property(e => e.F二阶段及时签收数量).HasColumnName("F二阶段及时签收数量").HasMaxLength(200);
        builder.Property(e => e.F二阶段及时签收率).HasColumnName("F二阶段及时签收率").HasMaxLength(200);
        builder.Property(e => e.F二阶段目标值).HasColumnName("F二阶段目标值").HasMaxLength(200);
        builder.Property(e => e.F二阶段预计考核金额).HasColumnName("F二阶段预计考核金额").HasMaxLength(200);
        builder.Property(e => e.F二阶段未及时签收数量).HasColumnName("F二阶段未及时签收数量").HasMaxLength(200);
        builder.Property(e => e.FT0延迟签收数量).HasColumnName("FT0延迟签收数量").HasMaxLength(200);
        builder.Property(e => e.FT1延迟签收数量).HasColumnName("FT1延迟签收数量").HasMaxLength(200);
        builder.Property(e => e.FT2延迟签收数量).HasColumnName("FT2延迟签收数量").HasMaxLength(200);
        builder.Property(e => e.FT3延迟签收数量).HasColumnName("FT3延迟签收数量").HasMaxLength(200);
        builder.Property(e => e.F当天考核数量).HasColumnName("F当天考核数量").HasMaxLength(200);
        builder.Property(e => e.F当天预估考核金额).HasColumnName("F当天预估考核金额").HasMaxLength(200);
        builder.Property(e => e.F当天签收及时量).HasColumnName("F当天签收及时量").HasMaxLength(200);
        builder.Property(e => e.F当天及时签收率).HasColumnName("F当天及时签收率").HasMaxLength(200);
        builder.Property(e => e.F当天目标值).HasColumnName("F当天目标值").HasMaxLength(200);
        builder.Property(e => e.F当天签收延迟24h内数量).HasColumnName("F当天签收延迟24h内数量").HasMaxLength(200);
        builder.Property(e => e.F当天签收延迟24至48h数量).HasColumnName("F当天签收延迟24至48h数量").HasMaxLength(200);
        builder.Property(e => e.F当天签收延迟超48h数量).HasColumnName("F当天签收延迟超48h数量").HasMaxLength(200);
        builder.Property(e => e.F当天签收延迟24h内率).HasColumnName("F当天签收延迟24h内率").HasMaxLength(200);
        builder.Property(e => e.F当天签收延迟24至48h率).HasColumnName("F当天签收延迟24至48h率").HasMaxLength(200);
        builder.Property(e => e.F当天签收延迟超48h率).HasColumnName("F当天签收延迟超48h率").HasMaxLength(200);
        builder.Property(e => e.F14点签收数量).HasColumnName("F14点签收数量").HasMaxLength(200);
        builder.Property(e => e.F20点签收数量).HasColumnName("F20点签收数量").HasMaxLength(200);
        builder.Property(e => e.F14点及时签收率).HasColumnName("F14点及时签收率").HasMaxLength(200);
        builder.Property(e => e.F20点及时签收率).HasColumnName("F20点及时签收率").HasMaxLength(200);
        builder.Property(e => e.F签收时长).HasColumnName("F签收时长").HasMaxLength(200);
        builder.Property(e => e.F网点时效用时).HasColumnName("F网点时效用时").HasMaxLength(200);
        builder.Property(e => e.F先签后派数量).HasColumnName("F先签后派数量").HasMaxLength(200);
        builder.Property(e => e.F先第三方后派数量).HasColumnName("F先第三方后派数量").HasMaxLength(200);
        builder.Property(e => e.F应签收数量).HasColumnName("F应签收数量").HasMaxLength(200);
        builder.Property(e => e.F已签收数量).HasColumnName("F已签收数量").HasMaxLength(200);
        builder.Property(e => e.F签收进度).HasColumnName("F签收进度").HasMaxLength(200);
        builder.Property(e => e.F未签收数量).HasColumnName("F未签收数量").HasMaxLength(200);
        builder.Property(e => e.F未签收有问题件数量).HasColumnName("F未签收有问题件数量").HasMaxLength(200);
        builder.Property(e => e.F已派件数量).HasColumnName("F已派件数量").HasMaxLength(200);
        builder.Property(e => e.F未派件数量).HasColumnName("F未派件数量").HasMaxLength(200);
        builder.Property(e => e.F已派未签数量).HasColumnName("F已派未签数量").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_末端派送网点汇总_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_末端派送网点汇总_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 末端派送 1 行/网点/日期，按「应签所属网点 + 统计日期」去重。
        builder.HasIndex(e => new { e.F应签所属网点, e.F统计日期, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F应签所属网点] IS NOT NULL AND [F应签所属网点] != ''")
            .HasDatabaseName("UX_STG申通_末端派送网点汇总_网点日期_未撤销");
    }
}
