using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_揽收分析明细 暂存表实体。
/// 来源：订单揽收分析明细V3（sheet「sheet1」）。
/// 24 个业务列全部先以 string? 落地（时间/数值列也是字符串，归一阶段再解析）。
/// 注意：本源主键列是「运单编号」（非「运单号」）；「订单揽收用时/h」含非法字符 /，dbColumn 去掉为 F订单揽收用时h。
/// </summary>
public class StgShentongPickupAnalysis : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongLogisticsCompleteness）
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

    // 业务字段（24 列，对应 sheet「sheet1」表头）
    public string? F统计日期 { get; set; }
    public string? F电商平台 { get; set; }
    public string? F运单编号 { get; set; }
    public string? F订单编号 { get; set; }
    public string? F时效类型 { get; set; }
    public string? F频次 { get; set; }
    public string? F订单时间 { get; set; }
    public string? F揽收时间 { get; set; }
    public string? F揽收截止时间 { get; set; }
    public string? F订单揽收用时h { get; set; }
    public string? F揽收标识 { get; set; }
    public string? F揽收及时标识 { get; set; }
    public string? F商家名称 { get; set; }
    public string? F订单网点 { get; set; }
    public string? F订单所属网点 { get; set; }
    public string? F揽收网点 { get; set; }
    public string? F揽收所属网点 { get; set; }
    public string? F收件员 { get; set; }
    public string? F订单始发城市 { get; set; }
    public string? F订单目的城市 { get; set; }
    public string? F仓类型 { get; set; }
    public string? F菜鸟仓编号 { get; set; }
    public string? F菜鸟仓名称 { get; set; }
    public string? F揽收超15天标识 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
