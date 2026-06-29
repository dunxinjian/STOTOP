using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongFakeSignConfiguration : IEntityTypeConfiguration<StgShentongFakeSign>
{
    public void Configure(EntityTypeBuilder<StgShentongFakeSign> builder)
    {
        builder.ToTable("STG申通_虚假签收明细");

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

        // 业务字段（34 列，全部可空字符串）
        builder.Property(e => e.F投诉日期).HasColumnName("F投诉日期").HasMaxLength(200);
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F工单号).HasColumnName("F工单号").HasMaxLength(200);
        builder.Property(e => e.F投诉时间).HasColumnName("F投诉时间").HasMaxLength(200);
        builder.Property(e => e.F省区编号).HasColumnName("F省区编号").HasMaxLength(200);
        builder.Property(e => e.F省区名称).HasColumnName("F省区名称").HasMaxLength(200);
        builder.Property(e => e.F省份编号).HasColumnName("F省份编号").HasMaxLength(200);
        builder.Property(e => e.F省份名称).HasColumnName("F省份名称").HasMaxLength(200);
        builder.Property(e => e.F所属网点编号).HasColumnName("F所属网点编号").HasMaxLength(200);
        builder.Property(e => e.F所属网点名称).HasColumnName("F所属网点名称").HasMaxLength(200);
        builder.Property(e => e.F被投诉网点编号).HasColumnName("F被投诉网点编号").HasMaxLength(200);
        builder.Property(e => e.F被投诉网点名称).HasColumnName("F被投诉网点名称").HasMaxLength(200);
        builder.Property(e => e.F派件业务员id).HasColumnName("F派件业务员id").HasMaxLength(200);
        builder.Property(e => e.F派件业务员名称).HasColumnName("F派件业务员名称").HasMaxLength(200);
        builder.Property(e => e.F投诉来源).HasColumnName("F投诉来源").HasMaxLength(200);
        builder.Property(e => e.F标签类型).HasColumnName("F标签类型").HasMaxLength(200);
        builder.Property(e => e.F电联履约状态).HasColumnName("F电联履约状态").HasMaxLength(200);
        builder.Property(e => e.F短信履约状态).HasColumnName("F短信履约状态").HasMaxLength(200);
        builder.Property(e => e.F客户声音履约状态).HasColumnName("F客户声音履约状态").HasMaxLength(200);
        builder.Property(e => e.F是否夜间签收).HasColumnName("F是否夜间签收").HasMaxLength(200);
        builder.Property(e => e.F签收类型).HasColumnName("F签收类型").HasMaxLength(200);
        builder.Property(e => e.F签收人).HasColumnName("F签收人").HasMaxLength(200);
        builder.Property(e => e.F代收点类型).HasColumnName("F代收点类型").HasMaxLength(200);
        builder.Property(e => e.F代收站点名称).HasColumnName("F代收站点名称").HasMaxLength(200);
        builder.Property(e => e.F签收时间).HasColumnName("F签收时间").HasMaxLength(200);
        builder.Property(e => e.F是否下发小件员任务).HasColumnName("F是否下发小件员任务").HasMaxLength(200);
        builder.Property(e => e.F小件员完结状态).HasColumnName("F小件员完结状态").HasMaxLength(200);
        builder.Property(e => e.F是否二次进线).HasColumnName("F是否二次进线").HasMaxLength(200);
        builder.Property(e => e.F是否时效件).HasColumnName("F是否时效件").HasMaxLength(200);
        builder.Property(e => e.F复核状态).HasColumnName("F复核状态").HasMaxLength(200);
        builder.Property(e => e.F复核状态说明).HasColumnName("F复核状态说明").HasMaxLength(200);
        builder.Property(e => e.F投诉类型).HasColumnName("F投诉类型").HasMaxLength(200);
        builder.Property(e => e.F投诉理由).HasColumnName("F投诉理由").HasMaxLength(200);
        builder.Property(e => e.F收件地址).HasColumnName("F收件地址").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_虚假签收明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_虚假签收明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 虚假签收明细每工单一行，按工单号去重；1 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F工单号, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F工单号] IS NOT NULL AND [F工单号] != ''")
            .HasDatabaseName("UX_STG申通_虚假签收明细_工单号_未撤销");
    }
}
