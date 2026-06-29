using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongHomeDeliveryConfiguration : IEntityTypeConfiguration<StgShentongHomeDelivery>
{
    public void Configure(EntityTypeBuilder<StgShentongHomeDelivery> builder)
    {
        builder.ToTable("STG申通_送货上门明细");

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
        builder.Property(e => e.F订单来源).HasColumnName("F订单来源").HasMaxLength(200);
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(200);
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F运单状态).HasColumnName("F运单状态").HasMaxLength(200);
        builder.Property(e => e.F承包区编号).HasColumnName("F承包区编号").HasMaxLength(200);
        builder.Property(e => e.F承包区名称).HasColumnName("F承包区名称").HasMaxLength(200);
        builder.Property(e => e.F业务员工号).HasColumnName("F业务员工号").HasMaxLength(200);
        builder.Property(e => e.F派送小件员名称).HasColumnName("F派送小件员名称").HasMaxLength(200);
        builder.Property(e => e.F回执情况).HasColumnName("F回执情况").HasMaxLength(200);
        builder.Property(e => e.F签收人信息).HasColumnName("F签收人信息").HasMaxLength(200);
        builder.Property(e => e.F履约情况).HasColumnName("F履约情况").HasMaxLength(200);
        builder.Property(e => e.F签收日期).HasColumnName("F签收日期").HasMaxLength(200);
        // Excel 列名「违规行为-二级内容」含非法字符 -，dbColumn 去掉为 F违规行为二级内容
        builder.Property(e => e.F违规行为二级内容).HasColumnName("F违规行为二级内容").HasMaxLength(200);
        builder.Property(e => e.F工单判罚类型).HasColumnName("F工单判罚类型").HasMaxLength(200);
        builder.Property(e => e.F是否电联).HasColumnName("F是否电联").HasMaxLength(200);
        builder.Property(e => e.F是否接通).HasColumnName("F是否接通").HasMaxLength(200);
        // 电联录音为 URL，可能逗号分隔多条，长文本
        builder.Property(e => e.F电联录音).HasColumnName("F电联录音").HasMaxLength(int.MaxValue);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_送货上门明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_送货上门明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 送货上门明细按「运单号 + 统计日期」去重（同运单跨日重复达成统计）；2 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.F统计日期, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_送货上门明细_运单统计日期_未撤销");
    }
}
