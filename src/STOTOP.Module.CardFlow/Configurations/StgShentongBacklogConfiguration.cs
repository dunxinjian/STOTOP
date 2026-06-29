using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongBacklogConfiguration : IEntityTypeConfiguration<StgShentongBacklog>
{
    public void Configure(EntityTypeBuilder<StgShentongBacklog> builder)
    {
        builder.ToTable("STG申通_积压明细");

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

        // 业务字段（41 列，全部可空字符串）
        builder.Property(e => e.F业务日期).HasColumnName("F业务日期").HasMaxLength(200);
        builder.Property(e => e.F运单号).HasColumnName("F运单号").HasMaxLength(200);
        builder.Property(e => e.F大区名称).HasColumnName("F大区名称").HasMaxLength(200);
        builder.Property(e => e.F省区名称).HasColumnName("F省区名称").HasMaxLength(200);
        builder.Property(e => e.F省份名称).HasColumnName("F省份名称").HasMaxLength(200);
        builder.Property(e => e.F所属网点编码).HasColumnName("F所属网点编码").HasMaxLength(200);
        builder.Property(e => e.F所属网点名称).HasColumnName("F所属网点名称").HasMaxLength(200);
        builder.Property(e => e.F应签网点编码).HasColumnName("F应签网点编码").HasMaxLength(200);
        builder.Property(e => e.F应签网点).HasColumnName("F应签网点").HasMaxLength(200);
        builder.Property(e => e.F四级区域编码).HasColumnName("F四级区域编码").HasMaxLength(200);
        builder.Property(e => e.F四级区域).HasColumnName("F四级区域").HasMaxLength(200);
        builder.Property(e => e.F三段码).HasColumnName("F三段码").HasMaxLength(200);
        builder.Property(e => e.F最后扫描组织编码).HasColumnName("F最后扫描组织编码").HasMaxLength(200);
        builder.Property(e => e.F最后扫描组织名称).HasColumnName("F最后扫描组织名称").HasMaxLength(200);
        builder.Property(e => e.F最后扫描组织父级编码).HasColumnName("F最后扫描组织父级编码").HasMaxLength(200);
        builder.Property(e => e.F最后扫描组织父级名称).HasColumnName("F最后扫描组织父级名称").HasMaxLength(200);
        builder.Property(e => e.F最后扫描时间).HasColumnName("F最后扫描时间").HasMaxLength(200);
        builder.Property(e => e.F最后扫描类型).HasColumnName("F最后扫描类型").HasMaxLength(200);
        builder.Property(e => e.F最后扫描类型编码).HasColumnName("F最后扫描类型编码").HasMaxLength(200);
        builder.Property(e => e.F扫描员).HasColumnName("F扫描员").HasMaxLength(200);
        builder.Property(e => e.F扫描员编码).HasColumnName("F扫描员编码").HasMaxLength(200);
        builder.Property(e => e.F业务员).HasColumnName("F业务员").HasMaxLength(200);
        builder.Property(e => e.F业务员编码).HasColumnName("F业务员编码").HasMaxLength(200);
        builder.Property(e => e.F问题件一级类型).HasColumnName("F问题件一级类型").HasMaxLength(200);
        builder.Property(e => e.F问题件二级类型).HasColumnName("F问题件二级类型").HasMaxLength(200);
        builder.Property(e => e.F退回件标识).HasColumnName("F退回件标识").HasMaxLength(200);
        builder.Property(e => e.F积压1天标识).HasColumnName("F积压1天标识").HasMaxLength(200);
        builder.Property(e => e.F积压2天标识).HasColumnName("F积压2天标识").HasMaxLength(200);
        builder.Property(e => e.F积压3天标识).HasColumnName("F积压3天标识").HasMaxLength(200);
        builder.Property(e => e.F积压4天标识).HasColumnName("F积压4天标识").HasMaxLength(200);
        builder.Property(e => e.F积压5天标识).HasColumnName("F积压5天标识").HasMaxLength(200);
        builder.Property(e => e.F积压六6天标识).HasColumnName("F积压六6天标识").HasMaxLength(200);
        builder.Property(e => e.F积压7天标识).HasColumnName("F积压7天标识").HasMaxLength(200);
        builder.Property(e => e.F积压815天标识).HasColumnName("F积压815天标识").HasMaxLength(200);
        builder.Property(e => e.F积压1630天标识).HasColumnName("F积压1630天标识").HasMaxLength(200);
        builder.Property(e => e.F积压3160天标识).HasColumnName("F积压3160天标识").HasMaxLength(200);
        builder.Property(e => e.F超过3天标识).HasColumnName("F超过3天标识").HasMaxLength(200);
        builder.Property(e => e.F超过5天标识).HasColumnName("F超过5天标识").HasMaxLength(200);
        builder.Property(e => e.F超过7天标识).HasColumnName("F超过7天标识").HasMaxLength(200);
        builder.Property(e => e.F是否积压剔除标识).HasColumnName("F是否积压剔除标识").HasMaxLength(200);
        builder.Property(e => e.F是否实时签收).HasColumnName("F是否实时签收").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_积压明细_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_积压明细_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 积压明细每运单一行，按运单号去重；1 字段以兼容 ExcelInputPlugin。
        builder.HasIndex(e => new { e.F运单号, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != ''")
            .HasDatabaseName("UX_STG申通_积压明细_运单号_未撤销");
    }
}
