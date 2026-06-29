using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_签收率考核汇总 暂存表实体。
/// 来源：签收率未达标考核（ossdfa...（签收率未达标考核）.xlsx，sheet Sheet1）。
/// 退化混合表头「坑」源：第 1 行前 4 列是真名（日期/网点编号/网点名称/所属省区），第 5 列起是分段表头
/// （应签量、48小时签收考核…168小时签收考核、总金额），分段子字段在第 2 行且跨段大量重复；第 3 行起才是数据。
/// 一期仅捕获第 1 行「全局唯一」的 6 列：日期/网点编号/网点名称/所属省区/应签量/总金额
/// （应签量取当日应签量、总金额取总考核金额）；分时段(48h/72h/96h/120h/144h/168h)明细因表头子字段重复
/// 无法按名映射，一期不逐列建模，余列由插件自动归集进 F其他列数据。
/// 6 个业务列全部先以 string? 落地（数值列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongSignRateAssess : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongPenetration）
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

    // 业务字段（仅 6 列：第 1 行全局唯一名；分时段明细未逐列建模）
    public string? F日期 { get; set; }
    public string? F网点编号 { get; set; }
    public string? F网点名称 { get; set; }
    public string? F所属省区 { get; set; }
    public string? F应签量 { get; set; }
    public string? F总金额 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
