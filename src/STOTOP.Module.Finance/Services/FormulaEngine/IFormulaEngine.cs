namespace STOTOP.Module.Finance.Services.FormulaEngine;

/// <summary>
/// 公式引擎接口
/// </summary>
public interface IFormulaEngine
{
    /// <summary>
    /// 求值公式
    /// </summary>
    decimal Evaluate(string formula, FormulaContext context);

    /// <summary>
    /// 验证公式语法
    /// </summary>
    bool Validate(string formula, out string error);
}
