using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongDeliveryAssessConfiguration : IEntityTypeConfiguration<StgShentongDeliveryAssess>
{
    public void Configure(EntityTypeBuilder<StgShentongDeliveryAssess> builder)
    {
        builder.ToTable("STG申通_末端派送考核明细");

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

        // 业务字段（63 列，全部可空字符串）
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(200);
        builder.Property(e => e.F中转站名称).HasColumnName("F中转站名称").HasMaxLength(200);
        builder.Property(e => e.F应签收所属网点名称).HasColumnName("F应签收所属网点名称").HasMaxLength(200);
        builder.Property(e => e.F应签收网点名称).HasColumnName("F应签收网点名称").HasMaxLength(200);
        builder.Property(e => e.F发件频次名称).HasColumnName("F发件频次名称").HasMaxLength(200);
        builder.Property(e => e.F中转站发件时间).HasColumnName("F中转站发件时间").HasMaxLength(200);
        builder.Property(e => e.F派件时间).HasColumnName("F派件时间").HasMaxLength(200);
        builder.Property(e => e.F签收时间).HasColumnName("F签收时间").HasMaxLength(200);
        builder.Property(e => e.F一阶段签收时限).HasColumnName("F一阶段签收时限").HasMaxLength(200);
        builder.Property(e => e.F一阶段内签收标识).HasColumnName("F一阶段内签收标识").HasMaxLength(200);
        builder.Property(e => e.F二阶段签收时限).HasColumnName("F二阶段签收时限").HasMaxLength(200);
        builder.Property(e => e.F二阶段内签收标识).HasColumnName("F二阶段内签收标识").HasMaxLength(200);
        builder.Property(e => e.F当天签收时限).HasColumnName("F当天签收时限").HasMaxLength(200);
        builder.Property(e => e.F当天签收标识).HasColumnName("F当天签收标识").HasMaxLength(200);
        builder.Property(e => e.F频次开始时间).HasColumnName("F频次开始时间").HasMaxLength(200);
        builder.Property(e => e.F频次截止时间).HasColumnName("F频次截止时间").HasMaxLength(200);
        builder.Property(e => e.F带货网点名称).HasColumnName("F带货网点名称").HasMaxLength(200);
        builder.Property(e => e.F派件员姓名).HasColumnName("F派件员姓名").HasMaxLength(200);
        builder.Property(e => e.F四级区域名称).HasColumnName("F四级区域名称").HasMaxLength(200);
        builder.Property(e => e.F五级区域名称).HasColumnName("F五级区域名称").HasMaxLength(200);
        builder.Property(e => e.F派件网点名称).HasColumnName("F派件网点名称").HasMaxLength(200);
        builder.Property(e => e.F签收网点名称).HasColumnName("F签收网点名称").HasMaxLength(200);
        builder.Property(e => e.F派次类型名称).HasColumnName("F派次类型名称").HasMaxLength(200);
        builder.Property(e => e.F签收类型名称).HasColumnName("F签收类型名称").HasMaxLength(200);
        builder.Property(e => e.F签收时长).HasColumnName("F签收时长").HasMaxLength(200);
        builder.Property(e => e.F网点时效用时).HasColumnName("F网点时效用时").HasMaxLength(200);
        builder.Property(e => e.F时效配置).HasColumnName("F时效配置").HasMaxLength(200);
        builder.Property(e => e.F发件日期).HasColumnName("F发件日期").HasMaxLength(200);
        builder.Property(e => e.FT0延迟签收标识).HasColumnName("FT0延迟签收标识").HasMaxLength(200);
        builder.Property(e => e.FT1延迟签收标识).HasColumnName("FT1延迟签收标识").HasMaxLength(200);
        builder.Property(e => e.FT2延迟签收标识).HasColumnName("FT2延迟签收标识").HasMaxLength(200);
        builder.Property(e => e.FT3延迟签收标识).HasColumnName("FT3延迟签收标识").HasMaxLength(200);
        builder.Property(e => e.F当天签收延迟024h标识).HasColumnName("F当天签收延迟024h标识").HasMaxLength(200);
        builder.Property(e => e.F当天签收延迟2448h标识).HasColumnName("F当天签收延迟2448h标识").HasMaxLength(200);
        builder.Property(e => e.F当天签收延迟超48h标识).HasColumnName("F当天签收延迟超48h标识").HasMaxLength(200);
        builder.Property(e => e.F14点签收时限).HasColumnName("F14点签收时限").HasMaxLength(200);
        builder.Property(e => e.F14点签收标识).HasColumnName("F14点签收标识").HasMaxLength(200);
        builder.Property(e => e.F20点签收时限).HasColumnName("F20点签收时限").HasMaxLength(200);
        builder.Property(e => e.F20点签收标识).HasColumnName("F20点签收标识").HasMaxLength(200);
        builder.Property(e => e.F已签收标识).HasColumnName("F已签收标识").HasMaxLength(200);
        builder.Property(e => e.F未签收有问题件标识).HasColumnName("F未签收有问题件标识").HasMaxLength(200);
        builder.Property(e => e.F已派未签标识).HasColumnName("F已派未签标识").HasMaxLength(200);
        builder.Property(e => e.F进村件标识).HasColumnName("F进村件标识").HasMaxLength(200);
        builder.Property(e => e.F有进村件配置标识).HasColumnName("F有进村件配置标识").HasMaxLength(200);
        builder.Property(e => e.F进村件顺延天数).HasColumnName("F进村件顺延天数").HasMaxLength(200);
        builder.Property(e => e.F问题件原因).HasColumnName("F问题件原因").HasMaxLength(200);
        builder.Property(e => e.F问题件类型名称).HasColumnName("F问题件类型名称").HasMaxLength(200);
        builder.Property(e => e.F问题件登记时间).HasColumnName("F问题件登记时间").HasMaxLength(200);
        builder.Property(e => e.F退回件原因).HasColumnName("F退回件原因").HasMaxLength(200);
        builder.Property(e => e.F退回件扫描时间).HasColumnName("F退回件扫描时间").HasMaxLength(200);
        builder.Property(e => e.F是否曾经退回标识).HasColumnName("F是否曾经退回标识").HasMaxLength(200);
        builder.Property(e => e.F是否曾经问题件标识).HasColumnName("F是否曾经问题件标识").HasMaxLength(200);
        builder.Property(e => e.F时效配置类型名称).HasColumnName("F时效配置类型名称").HasMaxLength(200);
        builder.Property(e => e.F未签收退回件标识).HasColumnName("F未签收退回件标识").HasMaxLength(200);
        builder.Property(e => e.F包号).HasColumnName("F包号").HasMaxLength(200);
        builder.Property(e => e.F预售标识).HasColumnName("F预售标识").HasMaxLength(200);
        builder.Property(e => e.F电商平台).HasColumnName("F电商平台").HasMaxLength(200);
        builder.Property(e => e.F配送类型名称).HasColumnName("F配送类型名称").HasMaxLength(200);
        builder.Property(e => e.F一阶段考核标识).HasColumnName("F一阶段考核标识").HasMaxLength(200);
        builder.Property(e => e.F二阶段考核标识).HasColumnName("F二阶段考核标识").HasMaxLength(200);
        builder.Property(e => e.F区域时效件).HasColumnName("F区域时效件").HasMaxLength(200);
        builder.Property(e => e.F三段码).HasColumnName("F三段码").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_末端派送考核明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_末端派送考核明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 末端派送考核明细每运单一行，按运单号去重；1 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_末端派送考核明细_运单号_未撤销");
    }
}
