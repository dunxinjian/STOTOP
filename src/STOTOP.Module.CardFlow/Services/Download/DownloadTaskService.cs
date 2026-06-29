using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.CardFlow.Services.Download;

public class DownloadTaskService : IDownloadTaskService
{
    private readonly STOTOPDbContext _context;
    private readonly DownloadJobService _jobService;
    private readonly IConfiguration _configuration;

    public DownloadTaskService(STOTOPDbContext context, DownloadJobService jobService, IConfiguration configuration)
    {
        _context = context;
        _jobService = jobService;
        _configuration = configuration;
    }

    public async Task<List<DownloadTaskDto>> GetListAsync()
    {
        return await _context.Set<CfDownloadTask>()
            .OrderByDescending(t => t.FCreatedTime)
            .Select(t => new DownloadTaskDto
            {
                Id = t.FID,
                TaskName = t.FTaskName,
                TargetUrl = t.FTargetUrl,
                LoginAccount = t.FLoginAccount,
                StoragePath = t.FStoragePath,
                CronExpression = t.FCronExpression,
                HangfireJobId = t.FHangfireJobId,
                Status = t.FStatus,
                CreateTime = t.FCreatedTime,
                UpdateTime = t.FUpdatedTime
            })
            .ToListAsync();
    }

    public async Task<DownloadTaskDetailDto?> GetByIdAsync(long id)
    {
        var task = await _context.Set<CfDownloadTask>()
            .FirstOrDefaultAsync(t => t.FID == id);

        if (task == null) return null;

        var steps = await _context.Set<CfDownloadStep>()
            .Where(s => s.FTaskId == id)
            .OrderBy(s => s.FSortOrder)
            .Select(s => new DownloadStepDto
            {
                Id = s.FID,
                SortOrder = s.FSortOrder,
                ActionType = s.FActionType,
                Selector = s.FSelector,
                Value = s.FValue,
                WaitTime = s.FWaitTime,
                Description = s.FDescription
            })
            .ToListAsync();

        return new DownloadTaskDetailDto
        {
            Id = task.FID,
            TaskName = task.FTaskName,
            TargetUrl = task.FTargetUrl,
            LoginAccount = task.FLoginAccount,
            Password = !string.IsNullOrEmpty(task.FLoginPassword)
                ? DbConnectionsHelper.DecryptPassword(task.FLoginPassword, _configuration.GetValue<string>("Security:EncryptionKey")!)
                : null,
            ScriptConfig = task.FScriptConfig,
            FilterConfig = task.FFilterConfig,
            StoragePath = task.FStoragePath,
            CronExpression = task.FCronExpression,
            HangfireJobId = task.FHangfireJobId,
            Status = task.FStatus,
            CreateTime = task.FCreatedTime,
            UpdateTime = task.FUpdatedTime,
            Steps = steps
        };
    }

    public async Task<DownloadTaskDetailDto> CreateAsync(DownloadTaskCreateDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var entity = new CfDownloadTask
            {
                FTaskName = dto.TaskName,
                FTargetUrl = dto.TargetUrl,
                FLoginAccount = dto.LoginAccount,
                FLoginPassword = !string.IsNullOrEmpty(dto.LoginPassword)
                    ? DbConnectionsHelper.EncryptPassword(dto.LoginPassword, _configuration.GetValue<string>("Security:EncryptionKey"))
                    : null,
                FScriptConfig = dto.ScriptConfig,
                FFilterConfig = dto.FilterConfig,
                FStoragePath = dto.StoragePath,
                FCronExpression = dto.CronExpression,
                FStatus = dto.Status,
                FCreatedTime = DateTime.Now
            };

            _context.Set<CfDownloadTask>().Add(entity);
            await _context.SaveChangesAsync();

            // 创建步骤
            if (dto.Steps?.Any() == true)
            {
                var steps = dto.Steps.Select(s => new CfDownloadStep
                {
                    FTaskId = entity.FID,
                    FSortOrder = s.SortOrder,
                    FActionType = s.ActionType,
                    FSelector = s.Selector,
                    FValue = s.Value,
                    FWaitTime = s.WaitTime,
                    FDescription = s.Description
                }).ToList();

                _context.Set<CfDownloadStep>().AddRange(steps);
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            // 如果有 Cron 表达式，注册定时任务
            if (!string.IsNullOrWhiteSpace(dto.CronExpression) && dto.Status == 1)
            {
                await _jobService.ScheduleTaskAsync(entity.FID);
            }

            return (await GetByIdAsync(entity.FID))!;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<DownloadTaskDetailDto> UpdateAsync(long id, DownloadTaskUpdateDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var entity = await _context.Set<CfDownloadTask>()
                .AsTracking()
                .FirstOrDefaultAsync(t => t.FID == id)
                ?? throw new InvalidOperationException("下载任务不存在");

            entity.FTaskName = dto.TaskName;
            entity.FTargetUrl = dto.TargetUrl;
            entity.FLoginAccount = dto.LoginAccount;
            if (!string.IsNullOrWhiteSpace(dto.LoginPassword))
                entity.FLoginPassword = DbConnectionsHelper.EncryptPassword(dto.LoginPassword, _configuration.GetValue<string>("Security:EncryptionKey"));
            entity.FScriptConfig = dto.ScriptConfig;
            entity.FFilterConfig = dto.FilterConfig;
            entity.FStoragePath = dto.StoragePath;
            entity.FCronExpression = dto.CronExpression;
            entity.FStatus = dto.Status;
            entity.FUpdatedTime = DateTime.Now;

            // 替换步骤：先删后增
            var oldSteps = await _context.Set<CfDownloadStep>()
                .Where(s => s.FTaskId == id)
                .ToListAsync();
            _context.Set<CfDownloadStep>().RemoveRange(oldSteps);

            if (dto.Steps?.Any() == true)
            {
                var newSteps = dto.Steps.Select(s => new CfDownloadStep
                {
                    FTaskId = id,
                    FSortOrder = s.SortOrder,
                    FActionType = s.ActionType,
                    FSelector = s.Selector,
                    FValue = s.Value,
                    FWaitTime = s.WaitTime,
                    FDescription = s.Description
                }).ToList();

                _context.Set<CfDownloadStep>().AddRange(newSteps);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // 更新定时任务
            if (!string.IsNullOrWhiteSpace(dto.CronExpression) && dto.Status == 1)
            {
                await _jobService.ScheduleTaskAsync(id);
            }
            else
            {
                await _jobService.UnscheduleTaskAsync(id);
            }

            return (await GetByIdAsync(id))!;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteAsync(long id)
    {
        // 取消定时任务
        await _jobService.UnscheduleTaskAsync(id);

        // 删除步骤
        var steps = await _context.Set<CfDownloadStep>()
            .Where(s => s.FTaskId == id)
            .ToListAsync();
        _context.Set<CfDownloadStep>().RemoveRange(steps);

        // 删除任务
        var task = await _context.Set<CfDownloadTask>()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == id);
        if (task != null)
        {
            _context.Set<CfDownloadTask>().Remove(task);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<DownloadLogDto>> GetLogsAsync(long? taskId = null)
    {
        var query = _context.Set<CfDownloadLog>().AsQueryable();

        if (taskId.HasValue)
            query = query.Where(l => l.FTaskId == taskId.Value);

        var logs = await query
            .OrderByDescending(l => l.FStartTime)
            .Take(200)
            .ToListAsync();

        // 获取任务名称映射
        var taskIds = logs.Select(l => l.FTaskId).Distinct().ToList();
        var taskNames = await _context.Set<CfDownloadTask>()
            .Where(t => taskIds.Contains(t.FID))
            .ToDictionaryAsync(t => t.FID, t => t.FTaskName);

        return logs.Select(l => new DownloadLogDto
        {
            Id = l.FID,
            TaskId = l.FTaskId,
            TaskName = taskNames.GetValueOrDefault(l.FTaskId),
            StartTime = l.FStartTime,
            EndTime = l.FEndTime,
            Status = l.FStatus,
            DownloadFileCount = l.FDownloadFileCount,
            FilePathList = l.FFilePathList,
            ErrorMessage = l.FErrorMessage
        }).ToList();
    }
}
