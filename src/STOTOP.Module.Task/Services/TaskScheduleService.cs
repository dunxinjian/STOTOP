using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;

namespace STOTOP.Module.Task.Services;

public class TaskScheduleService : ITaskScheduleService
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<TaskScheduleService> _logger;

    public TaskScheduleService(STOTOPDbContext db, ILogger<TaskScheduleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ApiResult<PagedResult<TaskScheduleListDto>>> GetPagedListAsync(SchedulePagedRequest request)
    {
        var query = _db.Set<TmTaskSchedule>()
            .Include(s => s.TemplateTask)
            .AsQueryable();

        if (request.ScheduleType.HasValue)
            query = query.Where(s => s.FScheduleType == request.ScheduleType.Value);

        if (request.IsEnabled.HasValue)
            query = query.Where(s => s.FIsEnabled == request.IsEnabled.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(s => s.TemplateTask != null && s.TemplateTask.FTitle.Contains(kw));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(s => s.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new TaskScheduleListDto
            {
                Id = s.FID,
                TemplateTaskId = s.FTemplateTaskId,
                TemplateTaskTitle = s.TemplateTask != null ? s.TemplateTask.FTitle : null,
                ScheduleType = s.FScheduleType,
                CronExpression = s.FCronExpression,
                ScheduledTime = s.FScheduledTime,
                NextExecution = s.FNextExecution,
                LastExecution = s.FLastExecution,
                IsEnabled = s.FIsEnabled,
                CreateTime = s.FCreateTime
            })
            .ToListAsync();

        return ApiResult<PagedResult<TaskScheduleListDto>>.Success(new PagedResult<TaskScheduleListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<TaskScheduleListDto>> CreateAsync(CreateTaskScheduleRequest request)
    {
        // 验证模板任务存在且为模板
        var templateTask = await _db.Set<TmTask>().FirstOrDefaultAsync(t => t.FID == request.TemplateTaskId);
        if (templateTask == null)
            return ApiResult<TaskScheduleListDto>.Fail("模板任务不存在");

        if (!templateTask.FIsTemplate)
            return ApiResult<TaskScheduleListDto>.Fail("关联的任务必须标记为模板任务");

        var schedule = new TmTaskSchedule
        {
            FTemplateTaskId = request.TemplateTaskId,
            FScheduleType = request.ScheduleType,
            FCronExpression = request.CronExpression,
            FScheduledTime = request.ScheduledTime,
            FIsEnabled = true,
            FCreateTime = DateTime.Now
        };

        // 计算下次执行时间
        schedule.FNextExecution = CalculateNextExecution(schedule);

        _db.Set<TmTaskSchedule>().Add(schedule);
        await _db.SaveChangesAsync();

        return ApiResult<TaskScheduleListDto>.Success(MapToDto(schedule, templateTask.FTitle));
    }

    public async Task<ApiResult<TaskScheduleListDto>> UpdateAsync(long id, UpdateTaskScheduleRequest request)
    {
        var schedule = await _db.Set<TmTaskSchedule>()
            .AsTracking()
            .Include(s => s.TemplateTask)
            .FirstOrDefaultAsync(s => s.FID == id);

        if (schedule == null)
            return ApiResult<TaskScheduleListDto>.Fail("调度不存在");

        schedule.FScheduleType = request.ScheduleType;
        schedule.FCronExpression = request.CronExpression;
        schedule.FScheduledTime = request.ScheduledTime;
        schedule.FIsEnabled = request.IsEnabled;

        // 重新计算下次执行时间
        schedule.FNextExecution = CalculateNextExecution(schedule);

        await _db.SaveChangesAsync();

        return ApiResult<TaskScheduleListDto>.Success(
            MapToDto(schedule, schedule.TemplateTask?.FTitle));
    }

    public async Task<ApiResult<bool>> ToggleAsync(long id)
    {
        var schedule = await _db.Set<TmTaskSchedule>()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.FID == id);

        if (schedule == null)
            return ApiResult<bool>.Fail("调度不存在");

        schedule.FIsEnabled = !schedule.FIsEnabled;

        // 启用时重新计算下次执行时间
        if (schedule.FIsEnabled)
            schedule.FNextExecution = CalculateNextExecution(schedule);

        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(schedule.FIsEnabled,
            schedule.FIsEnabled ? "已启用" : "已禁用");
    }

    public async global::System.Threading.Tasks.Task ExecuteScheduleAsync(long scheduleId)
    {
        var schedule = await _db.Set<TmTaskSchedule>()
            .AsTracking()
            .Include(s => s.TemplateTask)
            .FirstOrDefaultAsync(s => s.FID == scheduleId);

        if (schedule == null || !schedule.FIsEnabled || schedule.TemplateTask == null)
        {
            _logger.LogWarning("调度 {ScheduleId} 无效或已禁用，跳过执行", scheduleId);
            return;
        }

        var template = schedule.TemplateTask;

        // 基于模板任务创建新任务实例
        var newTask = new TmTask
        {
            FUID = Guid.NewGuid().ToString("N"),
            FTitle = template.FTitle,
            FDescription = template.FDescription,
            FOrgId = template.FOrgId,
            FProjectId = template.FProjectId,
            FGoalId = template.FGoalId,
            FKRId = template.FKRId,
            FParentTaskId = template.FParentTaskId,
            FType = template.FType,
            FPriority = template.FPriority,
            FStatus = 0, // 待办
            FAssigneeId = template.FAssigneeId,
            FCreatorId = template.FCreatorId,
            FPlanStart = DateTime.Now,
            FPlanEnd = template.FEstimatedHours.HasValue
                ? DateTime.Now.AddHours((double)template.FEstimatedHours.Value)
                : template.FPlanEnd,
            FEstimatedHours = template.FEstimatedHours,
            FProgress = 0,
            FVisibility = template.FVisibility,
            FIsTemplate = false,
            FSort = template.FSort,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<TmTask>().Add(newTask);

        // 更新调度执行时间
        schedule.FLastExecution = DateTime.Now;
        schedule.FNextExecution = CalculateNextExecution(schedule);

        // 一次性调度执行后自动禁用
        if (schedule.FScheduleType == 0)
            schedule.FIsEnabled = false;

        await _db.SaveChangesAsync();

        _logger.LogInformation("调度 {ScheduleId} 已执行，创建任务 {TaskId}（{TaskTitle}）",
            scheduleId, newTask.FID, newTask.FTitle);
    }

    /// <summary>
    /// 计算下次执行时间：定时(type=0)取FScheduledTime，周期(type=1)基于Cron表达式计算
    /// </summary>
    private static DateTime? CalculateNextExecution(TmTaskSchedule schedule)
    {
        if (schedule.FScheduleType == 0)
        {
            // 定时调度：直接使用设定时间
            return schedule.FScheduledTime;
        }

        if (schedule.FScheduleType == 1 && !string.IsNullOrWhiteSpace(schedule.FCronExpression))
        {
            try
            {
                var cron = CronExpression.Parse(schedule.FCronExpression);
                var next = cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Local);
                return next?.ToLocalTime();
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static TaskScheduleListDto MapToDto(TmTaskSchedule s, string? templateTaskTitle) => new()
    {
        Id = s.FID,
        TemplateTaskId = s.FTemplateTaskId,
        TemplateTaskTitle = templateTaskTitle,
        ScheduleType = s.FScheduleType,
        CronExpression = s.FCronExpression,
        ScheduledTime = s.FScheduledTime,
        NextExecution = s.FNextExecution,
        LastExecution = s.FLastExecution,
        IsEnabled = s.FIsEnabled,
        CreateTime = s.FCreateTime
    };
}
