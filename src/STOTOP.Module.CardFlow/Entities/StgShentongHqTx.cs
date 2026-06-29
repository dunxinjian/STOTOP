using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class StgShentongHqTx : BaseEntity, IStagingRecord
{
    // IStagingRecord 系统字段
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

    // 业务字段
    public string? F运单编号 { get; set; }
    public DateTime F业务日期 { get; set; }
    public DateTime? F记账日期 { get; set; }
    public string? F业务摘要 { get; set; }
    public string F网点编号 { get; set; } = string.Empty;
    public string F网点名称 { get; set; } = string.Empty;
    public string? F费用类型 { get; set; }
    public string F费用名称 { get; set; } = string.Empty;
    public decimal? F发生额收入 { get; set; }
    public decimal? F发生额支出 { get; set; }
    public decimal? F余额 { get; set; }
    public string? F账单类型 { get; set; }
    public string? F流水号 { get; set; }
    public string? F备注 { get; set; }
    public string? F结算方式 { get; set; }
    public string? F结算周期 { get; set; }
    public string? F操作人 { get; set; }
    public string? F科目编码 { get; set; }
}
