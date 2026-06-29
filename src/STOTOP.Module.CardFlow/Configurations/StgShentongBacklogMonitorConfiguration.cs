using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgShentongBacklogMonitorConfiguration : IEntityTypeConfiguration<StgShentongBacklogMonitor>
{
    public void Configure(EntityTypeBuilder<StgShentongBacklogMonitor> builder)
    {
        builder.ToTable("STG申通_积压监控汇总");

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

        // 业务字段（64 列；括号/连字符列 dbColumn 已去除非法字符）
        builder.Property(e => e.F统计日期).HasColumnName("F统计日期").HasMaxLength(200);
        builder.Property(e => e.F南北中部).HasColumnName("F南北中部").HasMaxLength(200);
        builder.Property(e => e.F大区).HasColumnName("F大区").HasMaxLength(200);
        builder.Property(e => e.F省区).HasColumnName("F省区").HasMaxLength(200);
        builder.Property(e => e.F省份).HasColumnName("F省份").HasMaxLength(200);
        builder.Property(e => e.F片区名称).HasColumnName("F片区名称").HasMaxLength(200);
        builder.Property(e => e.F片区管家).HasColumnName("F片区管家").HasMaxLength(200);
        builder.Property(e => e.F网点编码).HasColumnName("F网点编码").HasMaxLength(200);
        builder.Property(e => e.F网点名称).HasColumnName("F网点名称").HasMaxLength(200);
        builder.Property(e => e.F网点星级).HasColumnName("F网点星级").HasMaxLength(200);
        builder.Property(e => e.F代派网点编码).HasColumnName("F代派网点编码").HasMaxLength(200);
        builder.Property(e => e.F代派网点名称).HasColumnName("F代派网点名称").HasMaxLength(200);
        builder.Property(e => e.F异常状态).HasColumnName("F异常状态").HasMaxLength(200);
        builder.Property(e => e.F网点状态).HasColumnName("F网点状态").HasMaxLength(200);
        builder.Property(e => e.F拦截状态).HasColumnName("F拦截状态").HasMaxLength(200);
        builder.Property(e => e.F日均出港量).HasColumnName("F日均出港量").HasMaxLength(200);
        builder.Property(e => e.F日均进港量).HasColumnName("F日均进港量").HasMaxLength(200);
        builder.Property(e => e.F积压倍数).HasColumnName("F积压倍数").HasMaxLength(200);
        builder.Property(e => e.F15日累计积压量).HasColumnName("F15日累计积压量").HasMaxLength(200);
        builder.Property(e => e.F14日累计积压量).HasColumnName("F14日累计积压量").HasMaxLength(200);
        builder.Property(e => e.F积压实时数据).HasColumnName("F积压实时数据").HasMaxLength(200);
        builder.Property(e => e.F常态签收率).HasColumnName("F常态签收率").HasMaxLength(200);
        builder.Property(e => e.F近7天签收率).HasColumnName("F近7天签收率").HasMaxLength(200);
        builder.Property(e => e.F当天签收率).HasColumnName("F当天签收率").HasMaxLength(200);
        builder.Property(e => e.F进港量).HasColumnName("F进港量").HasMaxLength(200);
        builder.Property(e => e.F当天签收量).HasColumnName("F当天签收量").HasMaxLength(200);
        builder.Property(e => e.F清件进度).HasColumnName("F清件进度").HasMaxLength(200);
        builder.Property(e => e.F清件能力).HasColumnName("F清件能力").HasMaxLength(200);
        builder.Property(e => e.F近3天日均签收量).HasColumnName("F近3天日均签收量").HasMaxLength(200);
        builder.Property(e => e.F超3天积压量疑似遗失).HasColumnName("F超3天积压量疑似遗失").HasMaxLength(200);
        builder.Property(e => e.F超3天积压实时数量).HasColumnName("F超3天积压实时数量").HasMaxLength(200);
        builder.Property(e => e.F超3天积压占比).HasColumnName("F超3天积压占比").HasMaxLength(200);
        builder.Property(e => e.F超5天积压量智能遗失).HasColumnName("F超5天积压量智能遗失").HasMaxLength(200);
        builder.Property(e => e.F超5天积压实时数量).HasColumnName("F超5天积压实时数量").HasMaxLength(200);
        builder.Property(e => e.F超5天积压占比).HasColumnName("F超5天积压占比").HasMaxLength(200);
        builder.Property(e => e.F超7天积压量超长单).HasColumnName("F超7天积压量超长单").HasMaxLength(200);
        builder.Property(e => e.F超7天积压实时数量).HasColumnName("F超7天积压实时数量").HasMaxLength(200);
        builder.Property(e => e.F超7天积压占比).HasColumnName("F超7天积压占比").HasMaxLength(200);
        builder.Property(e => e.F积压1天量).HasColumnName("F积压1天量").HasMaxLength(200);
        builder.Property(e => e.F积压2天量).HasColumnName("F积压2天量").HasMaxLength(200);
        builder.Property(e => e.F积压3天量).HasColumnName("F积压3天量").HasMaxLength(200);
        builder.Property(e => e.F积压4天量).HasColumnName("F积压4天量").HasMaxLength(200);
        builder.Property(e => e.F积压5天量).HasColumnName("F积压5天量").HasMaxLength(200);
        builder.Property(e => e.F积压6天量).HasColumnName("F积压6天量").HasMaxLength(200);
        builder.Property(e => e.F积压6天实时数据).HasColumnName("F积压6天实时数据").HasMaxLength(200);
        builder.Property(e => e.F积压7天量).HasColumnName("F积压7天量").HasMaxLength(200);
        builder.Property(e => e.F积压815天量).HasColumnName("F积压815天量").HasMaxLength(200);
        builder.Property(e => e.F积压1630天量).HasColumnName("F积压1630天量").HasMaxLength(200);
        builder.Property(e => e.F积压3160天量).HasColumnName("F积压3160天量").HasMaxLength(200);
        builder.Property(e => e.F遗失率ppm).HasColumnName("F遗失率ppm").HasMaxLength(200);
        builder.Property(e => e.F遗失量).HasColumnName("F遗失量").HasMaxLength(200);
        builder.Property(e => e.F进港投诉量).HasColumnName("F进港投诉量").HasMaxLength(200);
        builder.Property(e => e.F进港投诉率).HasColumnName("F进港投诉率").HasMaxLength(200);
        builder.Property(e => e.F虚签投诉率上一周).HasColumnName("F虚签投诉率上一周").HasMaxLength(200);
        builder.Property(e => e.F7日虚签投诉量).HasColumnName("F7日虚签投诉量").HasMaxLength(200);
        builder.Property(e => e.Ft1虚签投诉量).HasColumnName("Ft1虚签投诉量").HasMaxLength(200);
        builder.Property(e => e.Ft2虚签投诉量).HasColumnName("Ft2虚签投诉量").HasMaxLength(200);
        builder.Property(e => e.Ft3虚签投诉量).HasColumnName("Ft3虚签投诉量").HasMaxLength(200);
        builder.Property(e => e.Ft4虚签投诉量).HasColumnName("Ft4虚签投诉量").HasMaxLength(200);
        builder.Property(e => e.Ft5虚签投诉量).HasColumnName("Ft5虚签投诉量").HasMaxLength(200);
        builder.Property(e => e.Ft6虚签投诉量).HasColumnName("Ft6虚签投诉量").HasMaxLength(200);
        builder.Property(e => e.Ft7虚签投诉量).HasColumnName("Ft7虚签投诉量").HasMaxLength(200);
        builder.Property(e => e.F剔除前累计积压量).HasColumnName("F剔除前累计积压量").HasMaxLength(200);
        builder.Property(e => e.F人工剔除量).HasColumnName("F人工剔除量").HasMaxLength(200);

        // 标准字段
        builder.Property(e => e.F其他列数据).HasColumnName("F其他列数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(500);
        builder.Property(e => e.F流水号).HasColumnName("F流水号").HasMaxLength(200);

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG申通_积压监控汇总_F批次ID");
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG申通_积压监控汇总_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");

        // 跨批次去重唯一索引（实际由 Seeder DDL 创建，此处声明保持模型一致性）
        // 积压监控 1 行/网点/日期，按「网点编码 + 统计日期」去重。
        builder.HasIndex(e => new { e.F网点编码, e.F统计日期, e.FOrgId })
            .IsUnique()
            .HasFilter("[FIsRevoked] = 0 AND [F网点编码] IS NOT NULL AND [F网点编码] != ''")
            .HasDatabaseName("UX_STG申通_积压监控汇总_网点日期_未撤销");
    }
}
