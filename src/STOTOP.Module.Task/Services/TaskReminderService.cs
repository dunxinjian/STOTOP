using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.Task.Events;

namespace STOTOP.Module.Task.Services;

public class TaskReminderService : ITaskReminderService
{
    private readonly STOTOPDbContext _db;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<TaskReminderService> _logger;

    public TaskReminderService(STOTOPDbContext db, IEventDispatcher eventDispatcher, ILogger<TaskReminderService> logger)
    {
        _db = db;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public async Task<ApiResult<List<TaskReminderListDto>>> GetListAsync(long taskId)
    {
        var items = await _db.Set<TmTaskReminder>()
            .Include(r => r.Task)
            .Where(r => r.FTaskId == taskId)
            .OrderBy(r => r.FReminderTime)
            .Select(r => new TaskReminderListDto
            {
                Id = r.FID,
                TaskId = r.FTaskId,
                TaskTitle = r.Task != null ? r.Task.FTitle : null,
                UserId = r.FUserId,
                ReminderTime = r.FReminderTime,
                ReminderType = r.FReminderType,
                IsRead = r.FIsRead,
                IsSent = r.FIsSent,
                CreateTime = r.FCreateTime
            })
            .ToListAsync();

        return ApiResult<List<TaskReminderListDto>>.Success(items);
    }

    public async Task<ApiResult<TaskReminderListDto>> CreateAsync(long taskId, CreateTaskReminderRequest request)
    {
        // 验证任务存在
        var task = await _db.Set<TmTask>().FirstOrDefaultAsync(t => t.FID == taskId);
        if (task == null)
            return ApiResult<TaskReminderListDto>.Fail("任务不存在");

        if (request.ReminderTime <= DateTime.Now)
            return ApiResult<TaskReminderListDto>.Fail("提醒时间必须晚于当前时间");

        var reminder = new TmTaskReminder
        {
            FTaskId = taskId,
            FUserId = request.UserId,
            FReminderTime = request.ReminderTime,
            FReminderType = request.ReminderType,
            FIsRead = false,
            FIsSent = false,
            FCreateTime = DateTime.Now
        };

        _db.Set<TmTaskReminder>().Add(reminder);
        await _db.SaveChangesAsync();

        return ApiResult<TaskReminderListDto>.Success(new TaskReminderListDto
        {
            Id = reminder.FID,
            TaskId = reminder.FTaskId,
            TaskTitle = task.FTitle,
            UserId = reminder.FUserId,
            ReminderTime = reminder.FReminderTime,
            ReminderType = reminder.FReminderType,
            IsRead = reminder.FIsRead,
            IsSent = reminder.FIsSent,
            CreateTime = reminder.FCreateTime
        });
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var reminder = await _db.Set<TmTaskReminder>().FirstOrDefaultAsync(r => r.FID == id);
        if (reminder == null)
            return ApiResult<bool>.Fail("提醒不存在");

        _db.Set<TmTaskReminder>().Remove(reminder);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "删除成功");
    }

    public async global::System.Threading.Tasks.Task ProcessDueRemindersAsync()
    {
        var now = DateTime.Now;

        var dueReminders = await _db.Set<TmTaskReminder>()
            .AsTracking()
            .Include(r => r.Task)
            .Where(r => !r.FIsSent && r.FReminderTime <= now)
            .ToListAsync();

        if (dueReminders.Count == 0)
            return;

        foreach (var reminder in dueReminders)
        {
            reminder.FIsSent = true;

            _logger.LogInformation(
                "提醒已标记为已发送：提醒ID={ReminderId}, 任务={TaskTitle}, 用户ID={UserId}, 提醒时间={ReminderTime}",
                reminder.FID,
                reminder.Task?.FTitle,
                reminder.FUserId,
                reminder.FReminderTime);

            // TODO: 对接通知服务（站内通知/钉钉推送）

            // 发布任务到期提醒事件
            try
            {
                await _eventDispatcher.PublishAsync(new TaskReminderDueEvent
                {
                    TaskId = reminder.FTaskId,
                    TaskTitle = reminder.Task?.FTitle ?? "",
                    AssigneeId = reminder.FUserId,
                    Deadline = reminder.FReminderTime,
                    TriggeredByUserId = 0, // 系统触发
                    ModuleCode = "task"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发布任务到期提醒事件失败，ReminderId={ReminderId}", reminder.FID);
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("本次共处理 {Count} 条到期提醒", dueReminders.Count);
    }
}
