using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_未出仓监控明细 暂存表实体。
/// 来源：未出仓实时监控导出（sheet「未出仓实时监控导出」）。
/// 13 个业务列全部先以 string? 落地（时间/数值列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongOutboundMonitor : BaseEntity, IStagingRecord
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

    // 业务字段（13 列，对应 sheet「未出仓实时监控导出」表头）
    public string? F统计日期 { get; set; }
    public string? F运单号 { get; set; }
    public string? F中转站 { get; set; }
    public string? F应签所属网点 { get; set; }
    public string? F应签所属网点编码 { get; set; }
    public string? F应签站点 { get; set; }
    public string? F应签站点编码 { get; set; }
    public string? F派件员 { get; set; }
    public string? F三段码 { get; set; }
    public string? F出仓距离 { get; set; }
    public string? F实际出仓时间 { get; set; }
    public string? F理论应出仓日期 { get; set; }
    public string? F理论应出仓时间 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
