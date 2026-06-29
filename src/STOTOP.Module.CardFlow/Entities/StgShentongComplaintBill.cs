using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_投诉账单明细 暂存表实体。
/// 来源：收到的投诉账单_账单明细（sheet「数据」，137 列、双行表头——第 1 行是分类合并表头
/// （费用明细数据/仲裁详情/申诉详情/申诉处理结果/申诉合计结果），第 2 行才是真字段名）。
/// 解析用 headerRow=2, dataStartRow=3。137 列中字段名跨段重复（如「责任方1网点名称」多段出现），
/// 插件按列名映射会被同名列覆盖，故只映射本次确认在 row2 中「名称全局唯一」的 23 列；
/// 其余 114 列由插件自动归集进 F其他列数据。23 列全部 string? 落地。
/// </summary>
public class StgShentongComplaintBill : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongSuspectedLoss）
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

    // 业务字段（23 列，均为 row2 中全局唯一的列名）
    public string? F运单号 { get; set; }
    public string? F账单一级类型 { get; set; }
    public string? F账单二级类型 { get; set; }
    public string? F金额 { get; set; }
    public string? F理赔来源 { get; set; }
    public string? F账单生成时间 { get; set; }
    public string? F申诉完结时间 { get; set; }
    public string? F理赔类型 { get; set; }
    public string? F处理结果 { get; set; }
    public string? F投诉网点 { get; set; }
    public string? F被投诉方1 { get; set; }
    public string? F完结方式 { get; set; }
    public string? F投诉时间 { get; set; }
    public string? F补录时间 { get; set; }
    public string? F内件品名 { get; set; }
    public string? F内件实际价值 { get; set; }
    public string? F调查经过 { get; set; }
    public string? F处理人 { get; set; }
    public string? F总部主管审核人姓名 { get; set; }
    public string? F受款方网点编号 { get; set; }
    public string? F受款方网点名称 { get; set; }
    public string? F受款方应受款金额 { get; set; }
    public string? F受款方协商受款金额 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
