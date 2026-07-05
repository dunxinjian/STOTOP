using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>
/// 钉钉应用配置（SYS钉钉配置，多租户阶段4·钉钉 per-tenant 地基）。
/// <para>
/// 每租户各自一套钉钉企业配置（corpId/appKey/appSecret/群机器人 webhook）。实现 <see cref="ITenantScoped"/> 进租户硬墙——
/// 防串租户配置、防伪造回调把他租户 corp 归到本租户。
/// </para>
/// <para>
/// 过渡期：根租户配置仍以 <c>dingtalk-config.json</c> 全局单份为权威（向后兼容，见 <see cref="Services.DingTalkTenantConfigService"/>）；
/// 本表承载【非根租户】的 per-tenant 配置，随 M8 多客户上线由平台/租户管理台写入。
/// </para>
/// </summary>
public class SysDingTalkConfig : BaseEntity, ITenantScoped
{
    /// <summary>租户ID（隔离键）</summary>
    public long FTenantId { get; set; }

    /// <summary>配置名称（业务展示用）</summary>
    public string FConfigName { get; set; } = "";

    /// <summary>企业 CorpId（钉钉企业标识）</summary>
    public string FCorpId { get; set; } = "";

    /// <summary>应用 AppKey</summary>
    public string FAppKey { get; set; } = "";

    /// <summary>应用 AppSecret（加密存储，与 dingtalk-config.json 同一 SecretProtector 密钥）</summary>
    public string FAppSecret { get; set; } = "";

    /// <summary>应用 AgentId（可选）</summary>
    public string? FAgentId { get; set; }

    /// <summary>自定义域名（可选，免登回调用）</summary>
    public string? FDomain { get; set; }

    /// <summary>群机器人 Webhook（可选，供告警/日报/周报 bot per-tenant 分发；M8 接入）</summary>
    public string? FRobotWebhookUrl { get; set; }

    /// <summary>是否启用（1=启用 / 0=停用）</summary>
    public int FIsEnabled { get; set; } = 1;

    /// <summary>是否自动同步（1=启用 / 0=停用）</summary>
    public int FAutoSync { get; set; }

    /// <summary>自动同步 Cron 表达式（quartz 格式）</summary>
    public string? FSyncCron { get; set; } = "0 0 2 * * ?";

    /// <summary>最后一次同步时间</summary>
    public DateTime? FLastSyncTime { get; set; }

    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
