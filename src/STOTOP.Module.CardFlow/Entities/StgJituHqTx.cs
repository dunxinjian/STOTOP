using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class StgJituHqTx : BaseEntity, IStagingRecord
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
    public string F流水号 { get; set; } = string.Empty;
    public string? F运单编号 { get; set; }
    public string? F账户ID { get; set; }
    public DateTime? F业务日期 { get; set; }
    public string? F所属网点 { get; set; }
    public string? F网点编号 { get; set; }
    public string F网点名称 { get; set; } = string.Empty;
    public string? F所属代理 { get; set; }
    public string F交易类型 { get; set; } = string.Empty;
    public string? F转运中心 { get; set; }
    public string? F结算中心 { get; set; }
    public string? F结算对象 { get; set; }
    public string F费用主类 { get; set; } = string.Empty;
    public string F费用子类 { get; set; } = string.Empty;
    public decimal? F发生金额 { get; set; }
    public decimal? F本次余额 { get; set; }
    public DateTime? F预付时间 { get; set; }
    public string? F备注 { get; set; }
}
