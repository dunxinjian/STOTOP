using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.WebAPI.Hubs;

namespace STOTOP.WebAPI.Services;

public class AutoPluginProgressReporter : IAutoPluginProgressReporter
{
    private static readonly int[] Milestones = { 1, 5, 10, 25, 50, 75, 100 };
    private readonly ConcurrentDictionary<string, int> _lastMilestones = new();
    private readonly IHubContext<ProgressHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;

    public AutoPluginProgressReporter(IHubContext<ProgressHub> hubContext, IServiceScopeFactory scopeFactory)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
    }

    public async Task ReportAsync(long batchId, string phase, int current, int total)
    {
        if (total <= 0) return;

        var percent = (int)((long)current * 100 / total);
        var milestone = GetCurrentMilestone(percent);
        var milestoneKey = GetMilestoneKey(batchId, phase);
        var last = _lastMilestones.GetOrAdd(milestoneKey, 0);

        if (milestone > last)
        {
            _lastMilestones[milestoneKey] = milestone;

            // 推送 SignalR
            await _hubContext.Clients.Group($"import-{batchId}").SendAsync("OnAutoPluginProgress", new
            {
                batchId,
                phase,
                current,
                total,
                percent = milestone
            });

            // 持久化到数据库
            await UpdateDbProgressAsync(batchId, phase, current, total);
        }
    }

    public async Task ReportPhaseAsync(long batchId, string phase, string message)
    {
        _lastMilestones[GetMilestoneKey(batchId, phase)] = 0;

        await _hubContext.Clients.Group($"import-{batchId}").SendAsync("OnAgentProgress", new
        {
            batchId,
            phase,
            current = 0,
            total = 0,
            percent = 0,
            message
        });

        await UpdateDbPhaseAsync(batchId, phase);
    }

    public void Reset(long batchId)
    {
        var prefix = $"{batchId}:";
        foreach (var key in _lastMilestones.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _lastMilestones.TryRemove(key, out _);
        }
    }

    private static string GetMilestoneKey(long batchId, string phase)
        => $"{batchId}:{phase}";

    private static int GetCurrentMilestone(int percent)
    {
        int result = 0;
        foreach (var m in Milestones)
        {
            if (percent >= m) result = m;
            else break;
        }
        return result;
    }

    private async Task UpdateDbProgressAsync(long batchId, string phase, int current, int total)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            var percent = total > 0 ? (int)(current * 100.0 / total) : 0;
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE [CF批次] SET [F当前节点名称] = {0}, [F进度百分比] = {1} WHERE FID = {2}",
                phase, percent, batchId);
        }
        catch { /* 进度更新失败不应中断主流程 */ }
    }

    private async Task UpdateDbPhaseAsync(long batchId, string phase)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE [CF批次] SET [F当前节点名称] = {0}, [F进度百分比] = 0 WHERE FID = {1}",
                phase, batchId);
        }
        catch { /* 进度更新失败不应中断主流程 */ }
    }
}
