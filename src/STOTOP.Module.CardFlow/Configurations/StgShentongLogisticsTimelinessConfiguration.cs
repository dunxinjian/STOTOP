using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongLogisticsTimelinessConfiguration : IEntityTypeConfiguration<StgShentongLogisticsTimeliness>
{
    public void Configure(EntityTypeBuilder<StgShentongLogisticsTimeliness> builder)
    {
        builder.ToTable("STG申通_物流及时准确明细");

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

        // 业务字段（17 列，全部可空字符串）
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(100);
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F网点名称).HasColumnName("F网点名称").HasMaxLength(200);
        builder.Property(e => e.F所属网点名称).HasColumnName("F所属网点名称").HasMaxLength(200);
        builder.Property(e => e.F问题类型).HasColumnName("F问题类型").HasMaxLength(50);
        builder.Property(e => e.F扫描时间).HasColumnName("F扫描时间").HasMaxLength(100);
        builder.Property(e => e.F上传时间).HasColumnName("F上传时间").HasMaxLength(100);
        builder.Property(e => e.F入库时间).HasColumnName("F入库时间").HasMaxLength(100);
        builder.Property(e => e.F扫描类型).HasColumnName("F扫描类型").HasMaxLength(100);
        builder.Property(e => e.F扫描员).HasColumnName("F扫描员").HasMaxLength(200);
        builder.Property(e => e.F扫描员编号).HasColumnName("F扫描员编号").HasMaxLength(100);
        builder.Property(e => e.F设备类型).HasColumnName("F设备类型").HasMaxLength(100);
        builder.Property(e => e.F设备ID).HasColumnName("F设备ID").HasMaxLength(100);
        builder.Property(e => e.F是否黑土共配).HasColumnName("F是否黑土共配").HasMaxLength(20);
        builder.Property(e => e.F订单平台).HasColumnName("F订单平台").HasMaxLength(100);
        builder.Property(e => e.F派件员编号).HasColumnName("F派件员编号").HasMaxLength(100);
        builder.Property(e => e.F派件员名称).HasColumnName("F派件员名称").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_物流及时准确明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_物流及时准确明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 按「事件身份」(运单号+问题类型) 去重、忽略导出统计日期；2 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.F问题类型, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_物流及时准确明细_运单问题_未撤销");
    }
}
