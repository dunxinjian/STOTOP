using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class StgShentongOutbound : BaseEntity, IStagingRecord
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
    public string? F所属网点 { get; set; }
    public string? F面单网点 { get; set; }
    public DateTime? F业务日期 { get; set; }
    public string? F店铺账号 { get; set; }
    public string? F共享别名 { get; set; }
    public decimal? F订单重量 { get; set; }
    public string? F结算对象 { get; set; }
    public string? F结算对象编号 { get; set; }
    public string? F结算类型 { get; set; }
    public decimal? F网点重量 { get; set; }
    public decimal? F集包重量 { get; set; }
    public decimal? F计泡重量 { get; set; }
    public decimal? F中心重量 { get; set; }
    public decimal? F总部重量 { get; set; }
    public decimal? F结算重量 { get; set; }
    public decimal? F三方重量 { get; set; }
    public decimal? F揽收重量 { get; set; }
    public decimal? F中转重量 { get; set; }
    public decimal? F到件重量 { get; set; }
    public decimal? F声明价值 { get; set; }
    public string? F目的省份 { get; set; }
    public string? F目的城市 { get; set; }
    public string? F一单到底 { get; set; }
    public int? F计算状态 { get; set; }
    public string? F签收网点 { get; set; }
    public decimal? F退回费 { get; set; }
    public string? F操作人 { get; set; }
}
