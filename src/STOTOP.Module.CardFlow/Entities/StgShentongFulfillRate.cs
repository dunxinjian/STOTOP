using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// STG申通_履约率明细 暂存表实体。
/// 来源：客户声音履约率导出（sheet「客户声音履约率」，单行表头，13 列）。
/// 13 个业务列全部先以 string? 落地（时间/标识列也是字符串，归一阶段再解析）。
/// </summary>
public class StgShentongFulfillRate : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段（照抄 StgShentongFakeSign）
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

    // 业务字段（13 列，对应 sheet「客户声音履约率」表头）
    public string? F日期 { get; set; }
    public string? F运单号 { get; set; }
    public string? F收件人 { get; set; }
    public string? F收件地址 { get; set; }
    public string? F履约要求 { get; set; }
    public string? F履约状态 { get; set; }
    public string? F是否虚假上门 { get; set; }
    public string? F小件员名称 { get; set; }
    public string? F小件员工号 { get; set; }
    public string? F签收时间 { get; set; }
    public string? F首次签收类型 { get; set; }
    public string? F签收人 { get; set; }
    public string? F服务要求 { get; set; }

    // 标准字段（与 ExcelInputPlugin / 建表 DDL 对齐）
    public string? F其他列数据 { get; set; }
    public string? F业务主键 { get; set; }
    public string? F流水号 { get; set; }
}
