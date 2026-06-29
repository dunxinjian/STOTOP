using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_物流完整性明细 暂存表实体。
/// 来源：未揽收/未到件/未派件 三个文件同结构（sheet「完整性明细」），靠「问题类型」列区分。
/// 17 个业务列全部先以 string? 落地（时间列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongLogisticsCompleteness : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongOutbound）
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

    // 业务字段（17 列，对应 sheet「完整性明细」表头）
    public string? F统计日期 { get; set; }
    public string? F运单号 { get; set; }
    public string? F网点名称 { get; set; }
    public string? F所属网点名称 { get; set; }
    public string? F问题类型 { get; set; }
    public string? F订单网点 { get; set; }
    public string? F订单平台 { get; set; }
    public string? F订单时间 { get; set; }
    public string? F揽收时间 { get; set; }
    public string? F揽收网点 { get; set; }
    public string? F派件时间 { get; set; }
    public string? F派件网点 { get; set; }
    public string? F签收时间 { get; set; }
    public string? F签收网点 { get; set; }
    public string? F是否黑土共配 { get; set; }
    public string? F签收员编号 { get; set; }
    public string? F签收员名称 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
