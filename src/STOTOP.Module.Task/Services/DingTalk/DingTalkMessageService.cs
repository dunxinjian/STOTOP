using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Services.DingTalk;

/// <summary>
/// 钉钉消息推送服务实现
/// </summary>
public class DingTalkMessageService : IDingTalkMessageService
{
    private readonly STOTOPDbContext _context;
    private readonly DingTalkApiClient _apiClient;
    private readonly ILogger<DingTalkMessageService> _logger;

    public DingTalkMessageService(
        STOTOPDbContext context,
        DingTalkApiClient apiClient,
        ILogger<DingTalkMessageService> logger)
    {
        _context = context;
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async global::System.Threading.Tasks.Task PushCommentAsync(long commentId)
    {
        try
        {
            var comment = await _context.Set<TmTaskComment>()
                .Include(c => c.Task)
                .FirstOrDefaultAsync(c => c.FID == commentId);

            if (comment == null)
            {
                _logger.LogWarning("评论不存在 - CommentId={CommentId}", commentId);
                return;
            }

            // 构建消息内容
            var taskTitle = comment.Task?.FTitle ?? "未知任务";
            var content = $"[任务评论] {taskTitle}\n{comment.FContent}";

            // 获取任务相关人员的钉钉 UserId 列表（排除评论发起者）
            var userIds = await GetTaskRelatedDingTalkUserIdsAsync(comment.FTaskId, comment.FUserId);

            // 创建消息推送记录
            var message = new TmDingTalkMessage
            {
                FSourceType = 0, // 0=评论
                FSourceId = commentId,
                FTaskId = comment.FTaskId,
                FSenderId = comment.FUserId,
                FPushStatus = 0, // 0=待推送
                FCreateTime = DateTime.Now
            };

            // 尝试推送
            if (userIds.Any())
            {
                var messageId = await _apiClient.SendWorkNotificationAsync(userIds, content);
                if (messageId != null)
                {
                    message.FDingTalkMessageId = messageId;
                    message.FPushStatus = 1; // 1=已推送
                }
            }
            else
            {
                _logger.LogInformation("评论推送：暂无目标用户钉钉ID，记录待推送 - CommentId={CommentId}", commentId);
            }

            _context.Set<TmDingTalkMessage>().Add(message);

            // 标记评论已推送钉钉
            comment.FPushedToDingTalk = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("推送评论到钉钉 - CommentId={CommentId}, PushStatus={Status}",
                commentId, message.FPushStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送评论到钉钉失败 - CommentId={CommentId}", commentId);
        }
    }

    /// <inheritdoc />
    public async global::System.Threading.Tasks.Task PushProgressAsync(long progressId)
    {
        try
        {
            var progress = await _context.Set<TmProgressReport>()
                .Include(p => p.Task)
                .FirstOrDefaultAsync(p => p.FID == progressId);

            if (progress == null)
            {
                _logger.LogWarning("进度上报不存在 - ProgressId={ProgressId}", progressId);
                return;
            }

            // 构建消息内容
            var taskTitle = progress.Task?.FTitle ?? "未知任务";
            var content = $"[进度上报] {taskTitle}\n进度：{progress.FProgress}%\n{progress.FContent}";

            // 获取任务相关人员的钉钉 UserId 列表（排除上报者）
            var userIds = await GetTaskRelatedDingTalkUserIdsAsync(progress.FTaskId, progress.FReporterId);

            // 创建消息推送记录
            var message = new TmDingTalkMessage
            {
                FSourceType = 1, // 1=进度上报
                FSourceId = progressId,
                FTaskId = progress.FTaskId,
                FSenderId = progress.FReporterId,
                FPushStatus = 0,
                FCreateTime = DateTime.Now
            };

            if (userIds.Any())
            {
                var messageId = await _apiClient.SendWorkNotificationAsync(userIds, content);
                if (messageId != null)
                {
                    message.FDingTalkMessageId = messageId;
                    message.FPushStatus = 1;
                }
            }
            else
            {
                _logger.LogInformation("进度推送：暂无目标用户钉钉ID，记录待推送 - ProgressId={ProgressId}", progressId);
            }

            _context.Set<TmDingTalkMessage>().Add(message);

            // 标记进度上报已推送钉钉
            progress.FPushedToDingTalk = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("推送进度上报到钉钉 - ProgressId={ProgressId}, PushStatus={Status}",
                progressId, message.FPushStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送进度上报到钉钉失败 - ProgressId={ProgressId}", progressId);
        }
    }

    /// <inheritdoc />
    public async global::System.Threading.Tasks.Task PushStatusChangeAsync(long taskId)
    {
        try
        {
            var task = await _context.Set<TmTask>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.FID == taskId);

            if (task == null)
            {
                _logger.LogWarning("任务不存在 - TaskId={TaskId}", taskId);
                return;
            }

            // 状态文本映射
            var statusText = task.FStatus switch
            {
                0 => "待开始",
                1 => "进行中",
                2 => "已完成",
                3 => "已取消",
                4 => "已暂停",
                _ => "未知"
            };

            var content = $"[状态变更] {task.FTitle}\n状态已变更为：{statusText}";

            // 获取任务相关人员的钉钉 UserId 列表
            var userIds = await GetTaskRelatedDingTalkUserIdsAsync(taskId, null);

            // 创建消息推送记录
            var message = new TmDingTalkMessage
            {
                FSourceType = 2, // 2=状态变更
                FSourceId = taskId,
                FTaskId = taskId,
                FSenderId = task.FCreatorId,
                FPushStatus = 0,
                FCreateTime = DateTime.Now
            };

            if (userIds.Any())
            {
                var messageId = await _apiClient.SendWorkNotificationAsync(userIds, content);
                if (messageId != null)
                {
                    message.FDingTalkMessageId = messageId;
                    message.FPushStatus = 1;
                }
            }
            else
            {
                _logger.LogInformation("状态变更推送：暂无目标用户钉钉ID，记录待推送 - TaskId={TaskId}", taskId);
            }

            _context.Set<TmDingTalkMessage>().Add(message);
            await _context.SaveChangesAsync();

            _logger.LogInformation("推送状态变更到钉钉 - TaskId={TaskId}, Status={Status}", taskId, statusText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送状态变更到钉钉失败 - TaskId={TaskId}", taskId);
        }
    }

    /// <inheritdoc />
    public async global::System.Threading.Tasks.Task ProcessPendingMessagesAsync()
    {
        try
        {
            // 查询所有待推送的消息（FPushStatus = 0）
            var pendingMessages = await _context.Set<TmDingTalkMessage>()
                .Where(m => m.FPushStatus == 0)
                .OrderBy(m => m.FCreateTime)
                .Take(100) // 每批最多处理 100 条
                .ToListAsync();

            if (!pendingMessages.Any())
            {
                _logger.LogDebug("没有待推送的钉钉消息");
                return;
            }

            _logger.LogInformation("开始处理 {Count} 条待推送的钉钉消息", pendingMessages.Count);

            foreach (var message in pendingMessages)
            {
                try
                {
                    // 根据消息类型重新构建推送内容
                    string? content = message.FSourceType switch
                    {
                        0 => await BuildCommentContentAsync(message.FSourceId),
                        1 => await BuildProgressContentAsync(message.FSourceId),
                        2 => await BuildStatusChangeContentAsync(message.FTaskId),
                        _ => null
                    };

                    if (content == null)
                    {
                        message.FPushStatus = 3; // 3=已失败（源数据不存在）
                        continue;
                    }

                    // 获取目标用户钉钉ID列表
                    var userIds = await GetTaskRelatedDingTalkUserIdsAsync(message.FTaskId, message.FSenderId);

                    if (userIds.Any())
                    {
                        var messageId = await _apiClient.SendWorkNotificationAsync(userIds, content);
                        if (messageId != null)
                        {
                            message.FDingTalkMessageId = messageId;
                            message.FPushStatus = 1; // 1=已推送
                        }
                    }
                    else
                    {
                        _logger.LogDebug("待推送消息 {MessageId} 暂无目标用户，保持待推送状态", message.FID);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理钉钉消息推送失败 - MessageId={MessageId}", message.FID);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("钉钉消息推送处理完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理待推送钉钉消息失败");
        }
    }

    #region Private Helpers

    /// <summary>
    /// 获取任务相关人员的钉钉 UserId 列表
    /// 包括：任务负责人、任务创建者、任务成员
    /// 排除指定的发送者（避免给自己发通知）
    /// </summary>
    private async Task<List<string>> GetTaskRelatedDingTalkUserIdsAsync(long taskId, long? excludeUserId)
    {
        // 1. 获取任务相关的系统用户ID（负责人、创建者、成员）
        var task = await _context.Set<TmTask>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.FID == taskId);

        if (task == null) return new List<string>();

        var relatedUserIds = new HashSet<long>();

        // 添加任务负责人
        if (task.FAssigneeId.HasValue)
            relatedUserIds.Add(task.FAssigneeId.Value);

        // 添加任务创建者
        relatedUserIds.Add(task.FCreatorId);

        // 添加任务成员
        var memberUserIds = await _context.Set<TmTaskMember>()
            .AsNoTracking()
            .Where(m => m.FTaskId == taskId)
            .Select(m => m.FUserId)
            .ToListAsync();

        foreach (var memberId in memberUserIds)
            relatedUserIds.Add(memberId);

        // 排除发送者自己
        if (excludeUserId.HasValue)
            relatedUserIds.Remove(excludeUserId.Value);

        if (!relatedUserIds.Any()) return new List<string>();

        // 2. 查询这些用户的钉钉 UserId（已绑定钉钉的用户）
        var dingTalkUserIds = await _context.Set<SysUser>()
            .AsNoTracking()
            .Where(u => relatedUserIds.Contains(u.FID)
                && u.FDingTalkBindStatus == 1
                && u.FDingTalkUserId != null)
            .Select(u => u.FDingTalkUserId!)
            .ToListAsync();

        return dingTalkUserIds;
    }

    private async Task<string?> BuildCommentContentAsync(long commentId)
    {
        var comment = await _context.Set<TmTaskComment>()
            .Include(c => c.Task)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.FID == commentId);

        if (comment?.Task == null) return null;
        return $"[任务评论] {comment.Task.FTitle}\n{comment.FContent}";
    }

    private async Task<string?> BuildProgressContentAsync(long progressId)
    {
        var progress = await _context.Set<TmProgressReport>()
            .Include(p => p.Task)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.FID == progressId);

        if (progress?.Task == null) return null;
        return $"[进度上报] {progress.Task.FTitle}\n进度：{progress.FProgress}%\n{progress.FContent}";
    }

    private async Task<string?> BuildStatusChangeContentAsync(long taskId)
    {
        var task = await _context.Set<TmTask>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.FID == taskId);

        if (task == null) return null;

        var statusText = task.FStatus switch
        {
            0 => "待开始",
            1 => "进行中",
            2 => "已完成",
            3 => "已取消",
            4 => "已暂停",
            _ => "未知"
        };

        return $"[状态变更] {task.FTitle}\n状态已变更为：{statusText}";
    }

    #endregion
}
