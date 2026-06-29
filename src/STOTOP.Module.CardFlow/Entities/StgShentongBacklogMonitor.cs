using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_积压监控汇总 暂存表实体。
/// 来源：积压异常监控（积压异常监控_导出任务_*.xlsx，sheet 积压异常监控）。
/// 单行表头，64 列，网点×日期粒度（1 行/网点/日期）。
/// 含括号列（遗失率（ppm)、超3天积压量（疑似遗失）、虚签投诉率(上一周) 等），dbColumn 去括号 → F遗失率ppm 等；
/// 另含连字符列（积压8-15天量、t-1虚签投诉量 等），dbColumn 去掉 '-' → F积压815天量 / Ft1虚签投诉量。
/// excelColumn 保留原文。64 个业务列全部先以 string? 落地（数值列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongBacklogMonitor : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongSignRateAssess）
    public long F批次ID { get; set; }
    public int F处理状态 { get; set; }
    public string? F错误信息 { get; set; }
    public long? F关联凭证ID { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    public string? FDataScopeId { get; set; }
    public long? FSourceWorkItemId { get; set; }
    public bool FIsRevoked { get; set; }
    public long FOrgId { get; set; }
    public long? F账套ID { get; set; }
    public string? F归属网点编号 { get; set; }

    // 业务字段（64 列；括号/连字符列 dbColumn 已去除非法字符）
    public string? F统计日期 { get; set; }
    public string? F南北中部 { get; set; }
    public string? F大区 { get; set; }
    public string? F省区 { get; set; }
    public string? F省份 { get; set; }
    public string? F片区名称 { get; set; }
    public string? F片区管家 { get; set; }
    public string? F网点编码 { get; set; }
    public string? F网点名称 { get; set; }
    public string? F网点星级 { get; set; }
    public string? F代派网点编码 { get; set; }
    public string? F代派网点名称 { get; set; }
    public string? F异常状态 { get; set; }
    public string? F网点状态 { get; set; }
    public string? F拦截状态 { get; set; }
    public string? F日均出港量 { get; set; }
    public string? F日均进港量 { get; set; }
    public string? F积压倍数 { get; set; }
    public string? F15日累计积压量 { get; set; }
    public string? F14日累计积压量 { get; set; }
    public string? F积压实时数据 { get; set; }
    public string? F常态签收率 { get; set; }
    public string? F近7天签收率 { get; set; }
    public string? F当天签收率 { get; set; }
    public string? F进港量 { get; set; }
    public string? F当天签收量 { get; set; }
    public string? F清件进度 { get; set; }
    public string? F清件能力 { get; set; }
    public string? F近3天日均签收量 { get; set; }
    public string? F超3天积压量疑似遗失 { get; set; }
    public string? F超3天积压实时数量 { get; set; }
    public string? F超3天积压占比 { get; set; }
    public string? F超5天积压量智能遗失 { get; set; }
    public string? F超5天积压实时数量 { get; set; }
    public string? F超5天积压占比 { get; set; }
    public string? F超7天积压量超长单 { get; set; }
    public string? F超7天积压实时数量 { get; set; }
    public string? F超7天积压占比 { get; set; }
    public string? F积压1天量 { get; set; }
    public string? F积压2天量 { get; set; }
    public string? F积压3天量 { get; set; }
    public string? F积压4天量 { get; set; }
    public string? F积压5天量 { get; set; }
    public string? F积压6天量 { get; set; }
    public string? F积压6天实时数据 { get; set; }
    public string? F积压7天量 { get; set; }
    public string? F积压815天量 { get; set; }
    public string? F积压1630天量 { get; set; }
    public string? F积压3160天量 { get; set; }
    public string? F遗失率ppm { get; set; }
    public string? F遗失量 { get; set; }
    public string? F进港投诉量 { get; set; }
    public string? F进港投诉率 { get; set; }
    public string? F虚签投诉率上一周 { get; set; }
    public string? F7日虚签投诉量 { get; set; }
    public string? Ft1虚签投诉量 { get; set; }
    public string? Ft2虚签投诉量 { get; set; }
    public string? Ft3虚签投诉量 { get; set; }
    public string? Ft4虚签投诉量 { get; set; }
    public string? Ft5虚签投诉量 { get; set; }
    public string? Ft6虚签投诉量 { get; set; }
    public string? Ft7虚签投诉量 { get; set; }
    public string? F剔除前累计积压量 { get; set; }
    public string? F人工剔除量 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
