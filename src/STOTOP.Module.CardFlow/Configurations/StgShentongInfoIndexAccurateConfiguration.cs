using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongInfoIndexAccurateConfiguration : IEntityTypeConfiguration<StgShentongInfoIndexAccurate>
{
    public void Configure(EntityTypeBuilder<StgShentongInfoIndexAccurate> builder)
    {
        builder.ToTable("STG申通_物流信息准确汇总");

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

        // 业务字段（14 列，全部可空字符串）
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(200);
        builder.Property(e => e.F网点名称).HasColumnName("F网点名称").HasMaxLength(200);
        builder.Property(e => e.F所属网点名称).HasColumnName("F所属网点名称").HasMaxLength(200);
        builder.Property(e => e.F揽收总量).HasColumnName("F揽收总量").HasMaxLength(200);
        builder.Property(e => e.F派件总量).HasColumnName("F派件总量").HasMaxLength(200);
        builder.Property(e => e.F签收总量).HasColumnName("F签收总量").HasMaxLength(200);
        builder.Property(e => e.F扫描单量).HasColumnName("F扫描单量").HasMaxLength(200);
        builder.Property(e => e.F问题单量).HasColumnName("F问题单量").HasMaxLength(200);
        builder.Property(e => e.F揽收晚于派件量).HasColumnName("F揽收晚于派件量").HasMaxLength(200);
        builder.Property(e => e.F揽收晚于签收量).HasColumnName("F揽收晚于签收量").HasMaxLength(200);
        builder.Property(e => e.F派件晚于签收量).HasColumnName("F派件晚于签收量").HasMaxLength(200);
        builder.Property(e => e.F不准确率).HasColumnName("F不准确率").HasMaxLength(200);
        builder.Property(e => e.F到件晚于签收量).HasColumnName("F到件晚于签收量").HasMaxLength(200);
        builder.Property(e => e.F到件不准确率).HasColumnName("F到件不准确率").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_物流信息准确汇总_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_物流信息准确汇总_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 物流信息准确汇总 1 行/网点/统计日期；本文件无网点编号，按「网点名称 + 统计日期」去重；2 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F网点名称, e.F统计日期, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F网点名称] IS NOT NULL AND [F网点名称] != ''")
            .HasDatabaseName("UX_STG申通_物流信息准确汇总_网点日期_未撤销");
    }
}
