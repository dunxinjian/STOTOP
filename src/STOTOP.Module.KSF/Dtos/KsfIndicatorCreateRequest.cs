using System.ComponentModel.DataAnnotations;

namespace STOTOP.Module.KSF.Dtos;

/// <summary>
/// KSF 指标创建/更新请求
/// </summary>
public class KsfIndicatorCreateRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    /// <summary>取数类型：1=SQL模板 2=Agent 3=常量值 4=外部API</summary>
    public int FetchType { get; set; } = 1;
    public string? FetchSql { get; set; }
    public string? FetchAgent { get; set; }
    public string? FetchParamsJson { get; set; }
    /// <summary>方向：1=正向 2=逆向</summary>
    public int Direction { get; set; } = 1;
    public string BizObjectType { get; set; } = "KSF";
    public bool IsEnabled { get; set; } = true;
}
