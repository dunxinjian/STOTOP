using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_签收未达标明细 暂存表实体。
/// 来源：签收未达标（sheet「数据」）。
/// 15 个业务列全部先以 string? 落地（时间/标识列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongSignSubstandard : BaseEntity, IStagingRecord
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

    // 业务字段（15 列，对应 sheet「数据」表头）
    public string? F运单号 { get; set; }
    public string? F应签网点 { get; set; }
    public string? F应签网点所属独立网点 { get; set; }
    public string? F应签日期 { get; set; }
    public string? F签收时间 { get; set; }
    public string? F业务员 { get; set; }
    public string? F当日签收标识 { get; set; }
    public string? F派件网点 { get; set; }
    public string? F签收网点 { get; set; }
    public string? F签收网点所属独立网点 { get; set; }
    public string? F是否已签收 { get; set; }
    public string? F是否未签收有问题件 { get; set; }
    public string? F是否曾经退回件 { get; set; }
    public string? F退回扫描时间 { get; set; }
    public string? F是否曾经问题件 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
