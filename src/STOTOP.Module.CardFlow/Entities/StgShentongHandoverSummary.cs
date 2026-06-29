using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_交货滞留汇总 暂存表实体。
/// 来源：网点交货滞留v3汇总（网点交货滞留v3汇总导出STO-*.xlsx，sheet sheet1）。
/// 单行表头，30 列，揽收所属网点×日期粒度（1 行/网点/日期）。
/// 含括号/连字符/'&' 列：交货平均用时(h) → F交货平均用时h（去括号）、
/// 考核滞留&揽收超48h量 → F考核滞留揽收超48h量（去 '&'）、
/// 揽收超48小时预估考核-日 → F揽收超48小时预估考核日（去 '-'）、滞留预估考核-日 → F滞留预估考核日（去 '-'）。
/// excelColumn 保留原文。30 个业务列全部先以 string? 落地（数值列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongHandoverSummary : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongBacklogMonitor）
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

    // 业务字段（30 列；括号/连字符/'&' 列 dbColumn 已去除非法字符）
    public string? F统计日期 { get; set; }
    public string? F揽收网点 { get; set; }
    public string? F揽收网点编码 { get; set; }
    public string? F揽收网点所属网点 { get; set; }
    public string? F揽收所属网点编码 { get; set; }
    public string? F揽收网点省区 { get; set; }
    public string? F揽收网点大区 { get; set; }
    public string? F揽收网点省份 { get; set; }
    public string? F中心名称 { get; set; }
    public string? F客户编码 { get; set; }
    public string? F客户名称 { get; set; }
    public string? F线路类型 { get; set; }
    public string? F原始揽收量 { get; set; }
    public string? F总揽收量 { get; set; }
    public string? F交货平均用时h { get; set; }
    public string? F交货及时量 { get; set; }
    public string? F交货延误量 { get; set; }
    public string? F总滞留量 { get; set; }
    public string? F未交货量 { get; set; }
    public string? F滞留率 { get; set; }
    public string? F目标值 { get; set; }
    public string? F揽收超48h总量 { get; set; }
    public string? F揽收超48h已交货量 { get; set; }
    public string? F揽收超48h未交货量 { get; set; }
    public string? F考核滞留揽收超48h量 { get; set; }
    public string? F揽收超48小时预估考核日 { get; set; }
    public string? F滞留预估考核日 { get; set; }
    public string? F考核滞留量 { get; set; }
    public string? F白名单标识 { get; set; }
    public string? F分频次标识 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
