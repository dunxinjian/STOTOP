using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class StgYundaHqTx : BaseEntity, IStagingRecord
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
    public string? F流水号 { get; set; }
    public string F运单编号 { get; set; } = string.Empty;
    public DateTime F业务日期 { get; set; }
    public string? F网点业务类型 { get; set; }
    public string? F所属业务类型 { get; set; }
    public string? F交易类型名称 { get; set; }
    public string F交易来源 { get; set; } = string.Empty;
    public string F交易月份 { get; set; } = string.Empty;
    public string? F到账时间 { get; set; }
    public string F收费公司 { get; set; } = string.Empty;
    public string F收费公司编码 { get; set; } = string.Empty;
    public string? F费用大类 { get; set; }
    public string? F收费项目 { get; set; }
    public string? F收费编码 { get; set; }
    public string? F三级收费科目 { get; set; }
    public string? F三级科目编码 { get; set; }
    public decimal F期初金额 { get; set; }
    public decimal F交易金额 { get; set; }
    public decimal F期末余额 { get; set; }
    public string F备注 { get; set; } = string.Empty;
    public string F结算状态 { get; set; } = string.Empty;
    public string F操作时间 { get; set; } = string.Empty;
}
