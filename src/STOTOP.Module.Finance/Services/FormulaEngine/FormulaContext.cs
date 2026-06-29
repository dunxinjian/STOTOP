namespace STOTOP.Module.Finance.Services.FormulaEngine;

/// <summary>
/// 公式求值上下文，包含科目金额、已计算行的结果和项目名称金额
/// </summary>
public class FormulaContext
{
    /// <summary>
    /// 科目编码 → 金额（支持前缀匹配）
    /// </summary>
    public Dictionary<string, decimal> AccountAmounts { get; set; } = new();

    /// <summary>
    /// 行号 → 已计算结果
    /// </summary>
    public Dictionary<int, decimal> RowResults { get; set; } = new();

    /// <summary>
    /// 项目名称 → 已计算金额（用于 ${项目名称} 引用求值）
    /// </summary>
    public Dictionary<string, decimal> ItemAmounts { get; set; } = new();
}
