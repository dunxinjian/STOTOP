using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_揽收考核汇总 暂存表实体。
/// 来源：订单揽收考核汇总分析（订单揽收考核汇总分析导出_v2-*.xlsx，sheet sheet1）。
/// 单行表头，19 列，揽收所属网点×日期粒度（1 行/网点/日期）。
/// 含末尾空格列：揽收承包区编码（原文末尾带空格）→ dbColumn 去尾空格 F揽收承包区编码；excelColumn 保留原始含空格文本。
/// 19 个业务列全部先以 string? 落地（数值列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongPickupAssess : BaseEntity, IStagingRecord
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

    // 业务字段（19 列；揽收承包区编码 列原文末尾带空格，dbColumn 已 trim）
    public string? F统计日期 { get; set; }
    public string? F电商平台 { get; set; }
    public string? F频次 { get; set; }
    public string? F时效类型 { get; set; }
    public string? F揽收大区 { get; set; }
    public string? F揽收省区 { get; set; }
    public string? F揽收省份 { get; set; }
    public string? F揽收所属网点 { get; set; }
    public string? F揽收所属网点编码 { get; set; }
    public string? F揽收承包区 { get; set; }
    public string? F揽收承包区编码 { get; set; }
    public string? F订单总量 { get; set; }
    public string? F及时揽收量 { get; set; }
    public string? F及时揽收率 { get; set; }
    public string? F未及时揽收量 { get; set; }
    public string? F未及时揽收率 { get; set; }
    public string? F揽收平均用时 { get; set; }
    public string? F先揽后下单量 { get; set; }
    public string? F超15天未揽收量 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
