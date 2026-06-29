using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongInboundComplaintConfiguration : IEntityTypeConfiguration<StgShentongInboundComplaint>
{
    public void Configure(EntityTypeBuilder<StgShentongInboundComplaint> builder)
    {
        builder.ToTable("STG申通_进港投诉明细");

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

        // 业务字段（29 列，全部可空字符串）
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(200);
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F投诉类型).HasColumnName("F投诉类型").HasMaxLength(200);
        builder.Property(e => e.F工单内容).HasColumnName("F工单内容").HasMaxLength(200);
        builder.Property(e => e.F大区名称).HasColumnName("F大区名称").HasMaxLength(200);
        builder.Property(e => e.F省区名称).HasColumnName("F省区名称").HasMaxLength(200);
        builder.Property(e => e.F行政省名称).HasColumnName("F行政省名称").HasMaxLength(200);
        builder.Property(e => e.F片区名称).HasColumnName("F片区名称").HasMaxLength(200);
        builder.Property(e => e.F所属网点编码).HasColumnName("F所属网点编码").HasMaxLength(200);
        builder.Property(e => e.F所属网点名称).HasColumnName("F所属网点名称").HasMaxLength(200);
        builder.Property(e => e.F承包区编码).HasColumnName("F承包区编码").HasMaxLength(200);
        builder.Property(e => e.F承包区名称).HasColumnName("F承包区名称").HasMaxLength(200);
        builder.Property(e => e.F小件员编码).HasColumnName("F小件员编码").HasMaxLength(200);
        builder.Property(e => e.F小件员名称).HasColumnName("F小件员名称").HasMaxLength(200);
        builder.Property(e => e.F工单类型编码).HasColumnName("F工单类型编码").HasMaxLength(200);
        builder.Property(e => e.F工单类型名称).HasColumnName("F工单类型名称").HasMaxLength(200);
        builder.Property(e => e.F工单源编码).HasColumnName("F工单源编码").HasMaxLength(200);
        builder.Property(e => e.F工单源名称).HasColumnName("F工单源名称").HasMaxLength(200);
        builder.Property(e => e.F工单创建时间).HasColumnName("F工单创建时间").HasMaxLength(200);
        builder.Property(e => e.F最后到件扫描时间).HasColumnName("F最后到件扫描时间").HasMaxLength(200);
        builder.Property(e => e.F到件扫描组织编码).HasColumnName("F到件扫描组织编码").HasMaxLength(200);
        builder.Property(e => e.F到件扫描组织名称).HasColumnName("F到件扫描组织名称").HasMaxLength(200);
        builder.Property(e => e.F签收时间).HasColumnName("F签收时间").HasMaxLength(200);
        builder.Property(e => e.F签收类型).HasColumnName("F签收类型").HasMaxLength(200);
        builder.Property(e => e.F代收点名称).HasColumnName("F代收点名称").HasMaxLength(200);
        builder.Property(e => e.F末端滞留天数).HasColumnName("F末端滞留天数").HasMaxLength(200);
        builder.Property(e => e.F是否按需派送标).HasColumnName("F是否按需派送标").HasMaxLength(200);
        builder.Property(e => e.F进港出港).HasColumnName("F进港出港").HasMaxLength(200);
        builder.Property(e => e.F差行为原因).HasColumnName("F差行为原因").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_进港投诉明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_进港投诉明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 进港投诉明细按「运单号 + 工单创建时间」去重；2 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.F工单创建时间, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_进港投诉明细_运单工单时间_未撤销");
    }
}
