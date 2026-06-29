using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Task.Services;

public class ProgressReportService : IProgressReportService
{
    private readonly STOTOPDbContext _db;

    public ProgressReportService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<ProgressReportListDto>>> GetPagedListAsync(long taskId, ProgressReportPagedRequest query)
    {
        var q = _db.Set<TmProgressReport>()
            .Where(r => r.FTaskId == taskId);

        if (query.ReporterId.HasValue)
            q = q.Where(r => r.FReporterId == query.ReporterId.Value);

        var total = await q.CountAsync();

        var reports = await q
            .OrderByDescending(r => r.FCreateTime)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var reportIds = reports.Select(r => r.FID).ToList();

        // 加载附件（RelationType=2 进度上报）
        var attachments = await _db.Set<TmAttachment>()
            .Where(a => a.FRelationType == 2 && reportIds.Contains(a.FRelationId))
            .ToListAsync();

        // 获取用户名
        var userIds = reports.Select(r => r.FReporterId)
            .Concat(attachments.Select(a => a.FUserId))
            .Distinct().ToList();
        var userDict = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToDictionaryAsync(u => u.FID, u => u.FName);

        var items = reports.Select(r => new ProgressReportListDto
        {
            Id = r.FID,
            TaskId = r.FTaskId,
            ReporterId = r.FReporterId,
            ReporterName = userDict.GetValueOrDefault(r.FReporterId),
            Progress = r.FProgress,
            Content = r.FContent,
            Hours = r.FHours,
            PushedToDingTalk = r.FPushedToDingTalk,
            CreateTime = r.FCreateTime,
            Attachments = attachments
                .Where(a => a.FRelationId == r.FID)
                .Select(a => new AttachmentListDto
                {
                    Id = a.FID,
                    RelationType = a.FRelationType,
                    RelationId = a.FRelationId,
                    UserId = a.FUserId,
                    UserName = userDict.GetValueOrDefault(a.FUserId),
                    OriginalFileName = a.FOriginalFileName,
                    StoragePath = a.FStoragePath,
                    FileSize = a.FFileSize,
                    FileType = a.FFileType,
                    CreateTime = a.FCreateTime
                }).ToList()
        }).ToList();

        return ApiResult<PagedResult<ProgressReportListDto>>.Success(new PagedResult<ProgressReportListDto>
        {
            Items = items,
            Total = total,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        });
    }

    public async Task<ApiResult<ProgressReportListDto>> CreateAsync(long taskId, CreateProgressReportRequest request, long operatorId)
    {
        var task = await _db.Set<TmTask>()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == taskId);

        if (task == null)
            return ApiResult<ProgressReportListDto>.Fail("任务不存在");

        var report = new TmProgressReport
        {
            FTaskId = taskId,
            FReporterId = operatorId,
            FProgress = request.Progress,
            FContent = request.Content,
            FHours = request.Hours,
            FCreateTime = DateTime.Now
        };

        _db.Set<TmProgressReport>().Add(report);

        // 自动更新任务进度
        task.FProgress = request.Progress;

        // 累加实际工时
        if (request.Hours.HasValue && request.Hours.Value > 0)
        {
            task.FActualHours = (task.FActualHours ?? 0) + request.Hours.Value;
        }

        task.FUpdateTime = DateTime.Now;

        // 记录活动日志
        _db.Set<TmActivityLog>().Add(new TmActivityLog
        {
            FTaskId = taskId,
            FActionType = 21, // 进度上报
            FOldValue = null,
            FNewValue = $"进度:{request.Progress}%,工时:{request.Hours?.ToString("0.0") ?? "0"}h",
            FOperatorId = operatorId,
            FRemark = "提交进度上报",
            FCreateTime = DateTime.Now
        });

        await _db.SaveChangesAsync();

        var userName = await _db.Set<SysUser>()
            .Where(u => u.FID == operatorId)
            .Select(u => u.FName)
            .FirstOrDefaultAsync();

        var dto = new ProgressReportListDto
        {
            Id = report.FID,
            TaskId = report.FTaskId,
            ReporterId = report.FReporterId,
            ReporterName = userName,
            Progress = report.FProgress,
            Content = report.FContent,
            Hours = report.FHours,
            PushedToDingTalk = report.FPushedToDingTalk,
            CreateTime = report.FCreateTime
        };

        return ApiResult<ProgressReportListDto>.Success(dto);
    }
}
