using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_虚签投诉明细 暂存表实体。
/// 来源：虚签投诉明细（sheet「虚签投诉明细」，单行表头，36 列）。
/// 36 个业务列全部先以 string? 落地（时间/金额/标识列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongFakeSignComplaint : BaseEntity, IStagingRecord
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

    // 业务字段（36 列，对应 sheet「虚签投诉明细」表头）
    public string? F投诉日期 { get; set; }
    public string? F运单号 { get; set; }
    public string? F投诉类型 { get; set; }
    public string? F投诉理由 { get; set; }
    public string? F工单号 { get; set; }
    public string? F投诉时间 { get; set; }
    public string? F省区编号 { get; set; }
    public string? F省区名称 { get; set; }
    public string? F省份编码 { get; set; }
    public string? F省份名称 { get; set; }
    public string? F所属网点编号 { get; set; }
    public string? F所属网点名称 { get; set; }
    public string? F被投诉网点编号 { get; set; }
    public string? F被投诉网点名称 { get; set; }
    public string? F派件业务员id { get; set; }
    public string? F派件业务员名称 { get; set; }
    public string? F投诉来源 { get; set; }
    public string? F标签类型 { get; set; }
    public string? F电联履约状态 { get; set; }
    public string? F短信履约状态 { get; set; }
    public string? F客户声音履约状态 { get; set; }
    public string? F是否夜间签收 { get; set; }
    public string? F预估考核金额 { get; set; }
    public string? F签收类型 { get; set; }
    public string? F签收人 { get; set; }
    public string? F代收点类型 { get; set; }
    public string? F代收点名称 { get; set; }
    public string? F签收时间 { get; set; }
    public string? F是否下发小件员任务 { get; set; }
    public string? F小件员完结状态 { get; set; }
    public string? F是否二次进线 { get; set; }
    public string? F是否时效件 { get; set; }
    public string? F复核状态 { get; set; }
    public string? F复核状态说明 { get; set; }
    public string? F收件地址 { get; set; }
    public string? F差行为原因 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
