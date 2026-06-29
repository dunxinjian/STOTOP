using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Enums;

namespace STOTOP.WebAPI.Jobs;

/// <summary>质量问题超时标记定时任务（每小时执行）</summary>
[AutomaticRetry(Attempts = 3)]
public class QualityOverdueCheckJob
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<QualityOverdueCheckJob> _logger;

    public QualityOverdueCheckJob(
        STOTOPDbContext context,
        ILogger<QualityOverdueCheckJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("开始执行质量问题超时检查...");
        try
        {
            var now = DateTime.UtcNow;

            // 1. 获取所有质量问题类型及其超时小时数（仅取 FTimeoutHours > 0 的）
            var issueTypes = await _context.Set<CfQualityIssueType>()
                .Where(t => t.FTimeoutHours > 0 && t.FStatus == 1)
                .Select(t => new { t.FCode, t.FTimeoutHours })
                .ToListAsync();

            if (!issueTypes.Any())
            {
                _logger.LogInformation("无有效质量问题类型配置超时，跳过");
                return;
            }

            // 2. 查询活跃的质量问题工作项（未完成、未取消、未已标记超时）
            var activeItems = await _context.Set<WfWorkItem>()
                .Where(w => w.FCategory == "QualityIssue"
                    && w.FStatus != (int)WorkItemStatus.Completed
                    && w.FStatus != (int)WorkItemStatus.Cancelled
                    && !w.FIsOverdue)
                .Select(w => new { w.FID, w.FBizType, w.FCreateTime })
                .ToListAsync();

            if (!activeItems.Any())
            {
                _logger.LogInformation("无活跃质量问题工作项需要检查");
                return;
            }

            // 3. 构建类型超时映射
            var timeoutMap = issueTypes.ToDictionary(t => t.FCode, t => t.FTimeoutHours, StringComparer.OrdinalIgnoreCase);

            // 4. 判断超时并收集需标记的工作项ID
            var overdueIds = new List<long>();
            foreach (var item in activeItems)
            {
                if (item.FBizType != null
                    && timeoutMap.TryGetValue(item.FBizType, out var timeoutHours)
                    && item.FCreateTime.AddHours(timeoutHours) < now)
                {
                    overdueIds.Add(item.FID);
                }
            }

            if (!overdueIds.Any())
            {
                _logger.LogInformation("本次检查无新增超时工作项");
                return;
            }

            // 5. 批量更新超时标记
            var updated = await _context.Set<WfWorkItem>()
                .Where(w => overdueIds.Contains(w.FID))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(w => w.FIsOverdue, true)
                    .SetProperty(w => w.FUpdateTime, DateTime.Now));

            _logger.LogInformation("质量问题超时检查完成，共标记 {Count} 条工作项为超时", updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "质量问题超时检查 Job 执行失败");
            throw; // 让 Hangfire 自动重试
        }
    }
}
