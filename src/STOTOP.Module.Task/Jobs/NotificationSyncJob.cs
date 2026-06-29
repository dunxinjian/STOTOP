using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Jobs;

/// <summary>
/// 通知同步Job：定期查询未推送到钉钉的通知并推送
/// Hangfire RecurringJob，每5分钟执行一次
/// </summary>
[AutomaticRetry(Attempts = 3)]
public class NotificationSyncJob
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<NotificationSyncJob> _logger;

    public NotificationSyncJob(STOTOPDbContext db, ILogger<NotificationSyncJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 执行通知同步：查询未推送钉钉的通知并推送
    /// </summary>
    public async global::System.Threading.Tasks.Task ExecuteAsync()
    {
        _logger.LogInformation("NotificationSyncJob 开始执行...");

        // 查询未推送到钉钉的通知（批量获取，每次最多100条）
        var pendingNotifications = await _db.Set<TmNotification>()
            .Where(n => !n.FPushedToDingTalk)
            .OrderBy(n => n.FCreateTime)
            .Take(100)
            .ToListAsync();

        if (pendingNotifications.Count == 0)
        {
            _logger.LogInformation("NotificationSyncJob 无待推送通知，跳过");
            return;
        }

        _logger.LogInformation("NotificationSyncJob 发现 {Count} 条待推送通知", pendingNotifications.Count);

        foreach (var notification in pendingNotifications)
        {
            try
            {
                // TODO: 预留钉钉推送接口调用位置
                // 1. 根据 notification.FReceiverId 查询用户的钉钉UserId
                // 2. 构建钉钉消息体（标题、内容、跳转链接等）
                // 3. 调用钉钉工作通知API推送
                // var dingTalkUserId = await GetDingTalkUserId(notification.FReceiverId);
                // await _dingTalkService.SendWorkNotification(dingTalkUserId, notification.FTitle, notification.FContent);

                // 标记为已推送
                notification.FPushedToDingTalk = true;

                _logger.LogDebug("通知 {Id} 推送成功（钉钉接口待集成）", notification.FID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通知 {Id} 推送失败", notification.FID);
                // 失败的通知不标记，下次继续重试
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("NotificationSyncJob 执行完成，处理 {Count} 条通知", pendingNotifications.Count);
    }
}
