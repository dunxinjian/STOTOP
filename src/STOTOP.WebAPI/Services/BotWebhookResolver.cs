using Microsoft.Extensions.Configuration;
using STOTOP.Core.Services;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.WebAPI.Services;

/// <summary>
/// 解析指定租户的群机器人 Webhook（+加签密钥）：优先 per-tenant 配置（SYS钉钉配置.F群机器人Webhook/Secret，
/// 根租户走 JSON 全局配置），根租户在 per-tenant 未设 webhook 时回退全局 appsettings（DingTalk:RobotWebhookUrl/RobotSecret）——
/// 向后兼容既有 bot 行为（现有 bot 一直读 appsettings，单客户下零回归）。供告警/日报/周报 bot 逐租户分发到各自钉钉群。
/// </summary>
public sealed class BotWebhookResolver
{
    private readonly IDingTalkTenantConfigService _cfg;
    private readonly ITenantResolver _resolver;
    private readonly IConfiguration _config;

    public BotWebhookResolver(IDingTalkTenantConfigService cfg, ITenantResolver resolver, IConfiguration config)
    {
        _cfg = cfg;
        _resolver = resolver;
        _config = config;
    }

    /// <summary>返回该租户群机器人的 (Webhook, Secret)；Webhook 为空表示该租户未配置群机器人（bot 应跳过该租户）。</summary>
    public async Task<(string? Webhook, string? Secret)> ResolveAsync(long tenantId)
    {
        var c = await _cfg.GetForTenantAsync(tenantId);
        var webhook = c?.RobotWebhookUrl;
        var secret = c?.RobotSecret;

        // 根租户未在 per-tenant 配置设群机器人 webhook → 回退全局 appsettings（现有 bot 一直读它，零回归）。
        // 显式 rootId.HasValue（与 DingTalkTenantConfigService 一致）：根租户解析不到时不回退（此时整条租户链已不可用）。
        var rootId = _resolver.GetRootTenantId();
        if (string.IsNullOrWhiteSpace(webhook) && rootId.HasValue && tenantId == rootId.Value)
        {
            webhook = _config["DingTalk:RobotWebhookUrl"];
            secret = _config["DingTalk:RobotSecret"];
        }

        return (webhook, secret);
    }
}
