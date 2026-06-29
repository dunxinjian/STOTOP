using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class StgExpenseRecord : BaseEntity, IStagingRecord
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
    public string? F数据ID { get; set; }
    public string? F流程类型 { get; set; }
    public string F费用类别 { get; set; } = string.Empty;
    public string? F费用摘要 { get; set; }
    public decimal F支出金额 { get; set; }
    public DateTime F业务日期 { get; set; }
    public string? F收款方 { get; set; }
    public string? F成本中心 { get; set; }
    public string? F审批编号 { get; set; }
    public string? F申请人 { get; set; }
    public string? F申请人部门 { get; set; }
    public string F审批结果 { get; set; } = string.Empty;
    public DateTime? F完成时间 { get; set; }
}
