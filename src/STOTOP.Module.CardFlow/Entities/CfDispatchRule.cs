using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 派发规则：定义批次/数据派发的触发条件与处理器（迁移自 CfDispatchRule）
/// </summary>
public class CfDispatchRule : BaseEntity
{
    /// <summary>规则名称</summary>
    public string FRuleName { get; set; } = string.Empty;
    /// <summary>触发事件：FlowCompleted 等</summary>
    public string FTriggerEvent { get; set; } = "FlowCompleted";
    /// <summary>适用暂存表 JSON 数组</summary>
    public string? FTargetTables { get; set; }
    /// <summary>规则类型：AlwaysMatch/RowLevel/BatchAggregate/HistoryCompare</summary>
    public string FRuleType { get; set; } = "AlwaysMatch";
    /// <summary>条件 JSON</summary>
    public string? FConditionJson { get; set; }
    /// <summary>严重级别：Info/Warning/Error/Critical</summary>
    public string FSeverity { get; set; } = "Info";
    /// <summary>处理器类型：AutoVoucher/WorkTask/AlertNotify/InfoRecord/Workflow</summary>
    public string FHandlerType { get; set; } = string.Empty;
    /// <summary>处理器配置 JSON</summary>
    public string? FHandlerConfigJson { get; set; }
    /// <summary>异步执行</summary>
    public bool FIsAsync { get; set; } = true;
    /// <summary>优先级</summary>
    public int FPriority { get; set; } = 100;
    /// <summary>状态</summary>
    public int FStatus { get; set; } = 1;
    /// <summary>说明</summary>
    public string? FDescription { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime? FUpdatedTime { get; set; }
}
