using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 自由派发配置：定义"某个流程完成后可以手动触发哪些流程"的关联关系。
/// 复合唯一约束：(F源流程编码, F目标流程编码, F组织ID)
/// </summary>
public class CfAdHocDispatchConfig : BaseEntity, IOrgScoped
{
    public string FSourceFlowCode { get; set; } = string.Empty;
    public string FTargetFlowCode { get; set; } = string.Empty;
    /// <summary>显示名称（如"发起付款"）</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>默认数据传递配置（三级协议）</summary>
    public string? FDataProtocolJson { get; set; }
    public long FOrgId { get; set; }
    public bool FIsEnabled { get; set; } = true;
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
