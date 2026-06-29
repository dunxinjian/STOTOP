using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongOutboundAssessConfiguration : IEntityTypeConfiguration<StgShentongOutboundAssess>
{
    public void Configure(EntityTypeBuilder<StgShentongOutboundAssess> builder)
    {
        builder.ToTable("STG申通_出仓考核汇总");

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

        // 业务字段（31 列；全角括号列 dbColumn 去括号）
        builder.Property(e => e.F所属网点编码).HasColumnName("F所属网点编码").HasMaxLength(200);
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(200);
        builder.Property(e => e.F省公司).HasColumnName("F省公司").HasMaxLength(200);
        builder.Property(e => e.F片区).HasColumnName("F片区").HasMaxLength(200);
        builder.Property(e => e.F所属网点).HasColumnName("F所属网点").HasMaxLength(200);
        builder.Property(e => e.F转运中心).HasColumnName("F转运中心").HasMaxLength(200);
        builder.Property(e => e.F派次类型).HasColumnName("F派次类型").HasMaxLength(200);
        builder.Property(e => e.F一频次考核应出仓日期).HasColumnName("F一频次考核应出仓日期").HasMaxLength(200);
        builder.Property(e => e.F一频次考核应出仓时间).HasColumnName("F一频次考核应出仓时间").HasMaxLength(200);
        builder.Property(e => e.F一频次考核应出仓量).HasColumnName("F一频次考核应出仓量").HasMaxLength(200);
        builder.Property(e => e.F一频次考核出仓及时量).HasColumnName("F一频次考核出仓及时量").HasMaxLength(200);
        builder.Property(e => e.F一频次考核出仓及时率).HasColumnName("F一频次考核出仓及时率").HasMaxLength(200);
        builder.Property(e => e.F一频次考核未及时出仓量).HasColumnName("F一频次考核未及时出仓量").HasMaxLength(200);
        builder.Property(e => e.F一频次考核考核目标).HasColumnName("F一频次考核考核目标").HasMaxLength(200);
        builder.Property(e => e.F一频次考核预估考核金额元).HasColumnName("F一频次考核预估考核金额元").HasMaxLength(200);
        builder.Property(e => e.F二频次监控应出仓日期).HasColumnName("F二频次监控应出仓日期").HasMaxLength(200);
        builder.Property(e => e.F二频次监控应出仓时间).HasColumnName("F二频次监控应出仓时间").HasMaxLength(200);
        builder.Property(e => e.F二频次监控应出仓量).HasColumnName("F二频次监控应出仓量").HasMaxLength(200);
        builder.Property(e => e.F二频次监控出仓及时量).HasColumnName("F二频次监控出仓及时量").HasMaxLength(200);
        builder.Property(e => e.F二频次监控出仓及时率).HasColumnName("F二频次监控出仓及时率").HasMaxLength(200);
        builder.Property(e => e.F二频次监控未及时出仓量).HasColumnName("F二频次监控未及时出仓量").HasMaxLength(200);
        builder.Property(e => e.F二频次监控考核目标).HasColumnName("F二频次监控考核目标").HasMaxLength(200);
        builder.Property(e => e.F二频次监控预估考核金额元).HasColumnName("F二频次监控预估考核金额元").HasMaxLength(200);
        builder.Property(e => e.F三频次监控应出仓日期).HasColumnName("F三频次监控应出仓日期").HasMaxLength(200);
        builder.Property(e => e.F三频次监控应出仓时间).HasColumnName("F三频次监控应出仓时间").HasMaxLength(200);
        builder.Property(e => e.F三频次监控应出仓量).HasColumnName("F三频次监控应出仓量").HasMaxLength(200);
        builder.Property(e => e.F三频次监控出仓及时量).HasColumnName("F三频次监控出仓及时量").HasMaxLength(200);
        builder.Property(e => e.F三频次监控出仓及时率).HasColumnName("F三频次监控出仓及时率").HasMaxLength(200);
        builder.Property(e => e.F三频次监控未及时出仓量).HasColumnName("F三频次监控未及时出仓量").HasMaxLength(200);
        builder.Property(e => e.F三频次监控考核目标).HasColumnName("F三频次监控考核目标").HasMaxLength(200);
        builder.Property(e => e.F三频次监控预估考核金额元).HasColumnName("F三频次监控预估考核金额元").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_出仓考核汇总_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_出仓考核汇总_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 出仓考核 1 行/网点/日期，按「所属网点编码 + 统计日期」去重。
        builder.HasIndex(e => new { e.F所属网点编码, e.F统计日期, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F所属网点编码] IS NOT NULL AND [F所属网点编码] != ''")
            .HasDatabaseName("UX_STG申通_出仓考核汇总_网点日期_未撤销");
    }
}
