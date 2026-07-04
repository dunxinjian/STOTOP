using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>外部身份供应商（多租户阶段4D·M8）。</summary>
public enum IdpProvider
{
    /// <summary>钉钉</summary>
    DingTalk = 1,
    /// <summary>企业微信（阶段4 声明桩，运行时未实装——需真企微 corp 凭证）</summary>
    WeCom = 2,
}

/// <summary>
/// 外部企业（IDP外部企业，M8）。一个外部 IdP 企业（钉钉/企微 corp）。
/// 平台层·不实现 <see cref="ITenantScoped"/>：一套 corp 可服务多租户（R4 N:N），归属由 IDP企业租户映射 承载。无 F组织ID → 漏标门禁不触发。
/// </summary>
public class IdpExternalCorp : BaseEntity
{
    /// <summary>供应商，见 <see cref="IdpProvider"/>：1=钉钉/2=企微</summary>
    public int FProvider { get; set; }

    /// <summary>企业 CorpId（供应商内唯一标识；本表唯一）</summary>
    public string FCorpId { get; set; } = string.Empty;

    /// <summary>企业名称</summary>
    public string FName { get; set; } = string.Empty;

    /// <summary>接入配置（加密 JSON：appKey/appSecret/agentId 等；沿用 Security:EncryptionKey 口径）</summary>
    public string? FAccessConfig { get; set; }

    public int FStatus { get; set; } = 1;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
