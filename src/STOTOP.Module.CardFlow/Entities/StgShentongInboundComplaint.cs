using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_进港投诉明细 暂存表实体。
/// 来源：进港投诉明细（sheet「进港投诉明细」，单行表头，29 列）。
/// 29 个业务列全部先以 string? 落地（时间/标识列也是字符串，归一阶段再解析）。
/// 注意：「进港/出港」含非法字符 /，dbColumn 去掉斜杠为 F进港出港（实体/EF/DDL/映射四处一致）。
/// </summary>
public class StgShentongInboundComplaint : BaseEntity, IStagingRecord
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

    // 业务字段（29 列，对应 sheet「进港投诉明细」表头）
    public string? F统计日期 { get; set; }
    public string? F运单号 { get; set; }
    public string? F投诉类型 { get; set; }
    public string? F工单内容 { get; set; }
    public string? F大区名称 { get; set; }
    public string? F省区名称 { get; set; }
    public string? F行政省名称 { get; set; }
    public string? F片区名称 { get; set; }
    public string? F所属网点编码 { get; set; }
    public string? F所属网点名称 { get; set; }
    public string? F承包区编码 { get; set; }
    public string? F承包区名称 { get; set; }
    public string? F小件员编码 { get; set; }
    public string? F小件员名称 { get; set; }
    public string? F工单类型编码 { get; set; }
    public string? F工单类型名称 { get; set; }
    public string? F工单源编码 { get; set; }
    public string? F工单源名称 { get; set; }
    public string? F工单创建时间 { get; set; }
    public string? F最后到件扫描时间 { get; set; }
    public string? F到件扫描组织编码 { get; set; }
    public string? F到件扫描组织名称 { get; set; }
    public string? F签收时间 { get; set; }
    public string? F签收类型 { get; set; }
    public string? F代收点名称 { get; set; }
    public string? F末端滞留天数 { get; set; }
    public string? F是否按需派送标 { get; set; }
    public string? F进港出港 { get; set; }
    public string? F差行为原因 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
