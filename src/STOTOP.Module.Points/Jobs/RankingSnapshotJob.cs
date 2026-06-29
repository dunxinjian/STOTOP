using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Points.Entities;
using STOTOP.Module.Points.Services;

namespace STOTOP.Module.Points.Jobs;

/// <summary>
/// 排名快照Job：每月1日1点执行
/// 汇总上月积分数据，生成排名快照记录
/// </summary>
[AutomaticRetry(Attempts = 3)]
public class RankingSnapshotJob
{
    private readonly STOTOPDbContext _db;
    private readonly IRankingService _rankingService;
    private readonly ILogger<RankingSnapshotJob> _logger;

    public RankingSnapshotJob(STOTOPDbContext db, IRankingService rankingService, ILogger<RankingSnapshotJob> logger)
    {
        _db = db;
        _rankingService = rankingService;
        _logger = logger;
    }

    /// <summary>
    /// 执行月度排名快照：汇总上月积分数据并生成排名记录
    /// </summary>
    public async global::System.Threading.Tasks.Task ExecuteAsync()
    {
        _logger.LogInformation("RankingSnapshotJob 开始执行...");

        // 计算上月周期标识，如 2026-03
        var lastMonth = DateTime.Now.AddMonths(-1);
        var period = lastMonth.ToString("yyyy-MM");

        // 获取所有组织ID
        var orgIds = await _db.Set<PmPointAccount>()
            .Select(a => a.FOrgId)
            .Distinct()
            .ToListAsync();

        if (orgIds.Count == 0)
        {
            _logger.LogInformation("RankingSnapshotJob 无积分账户数据，跳过");
            return;
        }

        foreach (var orgId in orgIds)
        {
            try
            {
                // 生成月度排名快照（dimension=0 表示月度）
                await _rankingService.GenerateSnapshotAsync(orgId, 0, period);
                _logger.LogInformation("RankingSnapshotJob 组织 {OrgId} 月度快照（{Period}）生成成功", orgId, period);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RankingSnapshotJob 组织 {OrgId} 快照生成失败", orgId);
            }
        }

        _logger.LogInformation("RankingSnapshotJob 执行完成，处理 {Count} 个组织", orgIds.Count);
    }
}
