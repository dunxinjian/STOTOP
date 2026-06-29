using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmPointRule : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FSourceId { get; set; }
    public string FRuleName { get; set; } = string.Empty;
    public string FRuleCode { get; set; } = string.Empty;
    public string FEventType { get; set; } = string.Empty;
    public int FPointValue { get; set; }
    public string? FConditionExpression { get; set; }
    public string? FConditionDescription { get; set; }
    public string? FMultiplierRule { get; set; }
    public int FCycleLimit { get; set; }
    public bool FRequireApproval { get; set; }
    public int FSortOrder { get; set; }
    public bool FIsEnabled { get; set; } = true;
    /// <summary>账户类型：1=A / 2=B（默认 B）</summary>
    public int F账户类型 { get; set; } = 2;
    /// <summary>清算策略：0=归零 / 1=转福利券 / 2=自定义</summary>
    public int F清算策略 { get; set; }
    /// <summary>转换比例（默认 1.0）</summary>
    public decimal F转换比例 { get; set; } = 1.0m;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
