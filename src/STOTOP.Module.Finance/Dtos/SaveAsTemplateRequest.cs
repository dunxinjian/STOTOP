namespace STOTOP.Module.Finance.Dtos;

/// <summary>
/// 将账套快照为模板的请求
/// </summary>
public class SaveAsTemplateRequest
{
    /// <summary>
    /// 源账套 FID
    /// </summary>
    public long AccountSetId { get; set; }

    /// <summary>
    /// 模板编码（如 express-delivery）
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 模板名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模板说明
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否标记为预置模板（仅超级管理员可设置 true）
    /// </summary>
    public bool IsPreset { get; set; }

    /// <summary>
    /// 同 Code 已存在时是否覆盖
    /// </summary>
    public bool Overwrite { get; set; }
}
