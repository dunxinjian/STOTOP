using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 政策返利奖罚规则
/// </summary>
public class ExpPolicyRebateRule : BaseEntity
{
    /// <summary>政策返利ID</summary>
    public long FPolicyRebateId { get; set; }
    /// <summary>规则类型 1均重 2单量 3重量段占比 4目的地占比 5计泡</summary>
    public int FRuleType { get; set; }
    /// <summary>规则名称</summary>
    public string FRuleName { get; set; } = string.Empty;
    /// <summary>启用</summary>
    public bool FEnabled { get; set; } = true;
    /// <summary>排序</summary>
    public int FSortOrder { get; set; }
}
