using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Services.DingTalk;

/// <summary>
/// 钉钉待办推送服务实现
/// </summary>
public class DingTalkTodoService : IDingTalkTodoService
{
    private readonly STOTOPDbContext _context;
    private readonly DingTalkApiClient _apiClient;
    private readonly ILogger<DingTalkTodoService> _logger;

    public DingTalkTodoService(
        STOTOPDbContext context,
        DingTalkApiClient apiClient,
        ILogger<DingTalkTodoService> logger)
    {
        _context = context;
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async global::System.Threading.Tasks.Task CreateTodoAsync(long taskId, long userId)
    {
        try
        {
            // 检查是否已存在待办记录
            var existing = await _context.Set<TmDingTalkTodo>()
                .FirstOrDefaultAsync(t => t.FTaskId == taskId && t.FUserId == userId);

            if (existing != null)
            {
                _logger.LogWarning("钉钉待办已存在 - TaskId={TaskId}, UserId={UserId}", taskId, userId);
                return;
            }

            // 获取任务信息
            var task = await _context.Set<TmTask>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.FID == taskId);

            if (task == null)
            {
                _logger.LogWarning("任务不存在 - TaskId={TaskId}", taskId);
                return;
            }

            // 查询用户的钉钉 UnionId
            var dingTalkUnionId = await GetDingTalkUnionIdAsync(userId);
            if (string.IsNullOrEmpty(dingTalkUnionId))
            {
                _logger.LogWarning("用户未绑定钉钉或钉钉UnionId为空 - UserId={UserId}", userId);
                // 仍然创建记录，标记为待同步，后续可通过 SyncPendingTodosAsync 补推
            }

            // 调用钉钉 API 创建待办
            string? dingTalkTodoId = null;
            if (!string.IsNullOrEmpty(dingTalkUnionId))
            {
                dingTalkTodoId = await _apiClient.CreateTodoAsync(
                    dingTalkUnionId, task.FTitle, task.FDescription, task.FPlanEnd);
            }

            // 创建待办记录
            var todo = new TmDingTalkTodo
            {
                FTaskId = taskId,
                FUserId = userId,
                FDingTalkTodoId = dingTalkTodoId,
                FSyncStatus = dingTalkTodoId != null ? 1 : 0, // 1=已同步, 0=待同步
                FCreateTime = DateTime.Now,
                FUpdateTime = DateTime.Now
            };

            _context.Set<TmDingTalkTodo>().Add(todo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("创建钉钉待办记录 - TaskId={TaskId}, UserId={UserId}, SyncStatus={Status}",
                taskId, userId, todo.FSyncStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建钉钉待办失败 - TaskId={TaskId}, UserId={UserId}", taskId, userId);
        }
    }

    /// <inheritdoc />
    public async global::System.Threading.Tasks.Task CompleteTodoAsync(long taskId, long userId)
    {
        try
        {
            var todo = await _context.Set<TmDingTalkTodo>()
                .FirstOrDefaultAsync(t => t.FTaskId == taskId && t.FUserId == userId);

            if (todo == null)
            {
                _logger.LogWarning("钉钉待办记录不存在 - TaskId={TaskId}, UserId={UserId}", taskId, userId);
                return;
            }

            // 调用钉钉 API 完成待办
            if (!string.IsNullOrEmpty(todo.FDingTalkTodoId))
            {
                var unionId = await GetDingTalkUnionIdAsync(userId);
                if (!string.IsNullOrEmpty(unionId))
                {
                    await _apiClient.CompleteTodoAsync(todo.FDingTalkTodoId, unionId);
                }
                else
                {
                    await _apiClient.CompleteTodoAsync(todo.FDingTalkTodoId);
                }
            }

            todo.FSyncStatus = 2; // 2=已完成
            todo.FUpdateTime = DateTime.Now;
            await _context.SaveChangesAsync();

            _logger.LogInformation("完成钉钉待办 - TaskId={TaskId}, UserId={UserId}", taskId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "完成钉钉待办失败 - TaskId={TaskId}, UserId={UserId}", taskId, userId);
        }
    }

    /// <inheritdoc />
    public async global::System.Threading.Tasks.Task CancelTodoAsync(long taskId, long userId)
    {
        try
        {
            var todo = await _context.Set<TmDingTalkTodo>()
                .FirstOrDefaultAsync(t => t.FTaskId == taskId && t.FUserId == userId);

            if (todo == null)
            {
                _logger.LogWarning("钉钉待办记录不存在 - TaskId={TaskId}, UserId={UserId}", taskId, userId);
                return;
            }

            // 调用钉钉 API 取消待办
            if (!string.IsNullOrEmpty(todo.FDingTalkTodoId))
            {
                var unionId = await GetDingTalkUnionIdAsync(userId);
                if (!string.IsNullOrEmpty(unionId))
                {
                    await _apiClient.CancelTodoAsync(todo.FDingTalkTodoId, unionId);
                }
                else
                {
                    await _apiClient.CancelTodoAsync(todo.FDingTalkTodoId);
                }
            }

            todo.FSyncStatus = 3; // 3=已取消
            todo.FUpdateTime = DateTime.Now;
            await _context.SaveChangesAsync();

            _logger.LogInformation("取消钉钉待办 - TaskId={TaskId}, UserId={UserId}", taskId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消钉钉待办失败 - TaskId={TaskId}, UserId={UserId}", taskId, userId);
        }
    }

    /// <inheritdoc />
    public async global::System.Threading.Tasks.Task SyncPendingTodosAsync()
    {
        try
        {
            // 查询所有待同步的记录（FSyncStatus = 0）
            var pendingTodos = await _context.Set<TmDingTalkTodo>()
                .Include(t => t.Task)
                .Where(t => t.FSyncStatus == 0)
                .ToListAsync();

            if (!pendingTodos.Any())
            {
                _logger.LogDebug("没有待同步的钉钉待办");
                return;
            }

            _logger.LogInformation("开始同步 {Count} 条待处理的钉钉待办", pendingTodos.Count);

            foreach (var todo in pendingTodos)
            {
                try
                {
                    if (todo.Task == null) continue;

                    // 查询用户的钉钉 UnionId
                    var dingTalkUnionId = await GetDingTalkUnionIdAsync(todo.FUserId);
                    if (string.IsNullOrEmpty(dingTalkUnionId))
                    {
                        _logger.LogDebug("用户未绑定钉钉，跳过同步 - UserId={UserId}", todo.FUserId);
                        continue;
                    }

                    var dingTalkTodoId = await _apiClient.CreateTodoAsync(
                        dingTalkUnionId, todo.Task.FTitle, todo.Task.FDescription, todo.Task.FPlanEnd);

                    if (dingTalkTodoId != null)
                    {
                        todo.FDingTalkTodoId = dingTalkTodoId;
                        todo.FSyncStatus = 1;
                        todo.FUpdateTime = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "同步钉钉待办失败 - TodoId={TodoId}", todo.FID);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("钉钉待办同步完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步待处理钉钉待办失败");
        }
    }

    #region Private Helpers

    /// <summary>
    /// 根据系统用户ID查询钉钉 UnionId
    /// </summary>
    private async Task<string?> GetDingTalkUnionIdAsync(long userId)
    {
        var user = await _context.Set<SysUser>()
            .AsNoTracking()
            .Where(u => u.FID == userId)
            .Select(u => new { u.FDingTalkUnionId, u.FDingTalkBindStatus })
            .FirstOrDefaultAsync();

        if (user == null || user.FDingTalkBindStatus != 1)
            return null;

        return user.FDingTalkUnionId;
    }

    #endregion
}
