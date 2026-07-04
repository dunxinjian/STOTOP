using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services;

/// <summary>
/// <see cref="ITenantTodoChannelResolver"/> 默认实现：读 PLT租户.FDefaultTodoChannel(平台层表、无租户过滤器)映射渠道名。
/// 单客户下 FDefaultTodoChannel=1 → ["dingtalk"]（与现状等价）；企微=声明桩(渠道未注册)故"双推"当前实际只到钉钉。
/// </summary>
public class TenantTodoChannelResolver : ITenantTodoChannelResolver
{
    private const string DingTalk = "dingtalk";
    private const string WeCom = "wecom";

    private readonly STOTOPDbContext _db;

    public TenantTodoChannelResolver(STOTOPDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> ResolveChannelNamesAsync(long tenantId)
    {
        var channel = await _db.Set<PltTenant>().AsNoTracking()
            .Where(t => t.FID == tenantId)
            .Select(t => (int?)t.FDefaultTodoChannel)
            .FirstOrDefaultAsync();

        return channel switch
        {
            1 => new[] { DingTalk },
            2 => new[] { WeCom },
            3 => new[] { DingTalk, WeCom },   // 双推（企微桩未注册时调用方过滤后实际只到钉钉）
            _ => Array.Empty<string>(),        // 无 PLT租户 行/未知 → 空，调用方回退按待办自带渠道
        };
    }
}
