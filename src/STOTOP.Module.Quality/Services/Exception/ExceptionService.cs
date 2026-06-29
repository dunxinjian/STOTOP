using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Entities;
using STOTOP.Module.Quality.Events;

namespace STOTOP.Module.Quality.Services.Exception;

public class ExceptionService : IExceptionService
{
    private readonly STOTOPDbContext _db;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<ExceptionService> _logger;

    public ExceptionService(STOTOPDbContext db, IEventDispatcher eventDispatcher, ILogger<ExceptionService> logger)
    {
        _db = db;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public async Task<ApiResult<PagedResult<ExceptionListDto>>> GetPagedAsync(long orgId, ExceptionPagedRequest request)
    {
        var query = _db.Set<QlException>().Where(e => e.FOrgId == orgId);

        if (request.Type.HasValue)
            query = query.Where(e => e.FType == request.Type.Value);
        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);
        if (request.Priority.HasValue)
            query = query.Where(e => e.FPriority == request.Priority.Value);
        if (request.AssigneeId.HasValue)
            query = query.Where(e => e.FAssigneeId == request.AssigneeId.Value);
        if (request.StartDate.HasValue)
            query = query.Where(e => e.FCreateTime >= request.StartDate.Value);
        if (request.EndDate.HasValue)
            query = query.Where(e => e.FCreateTime <= request.EndDate.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(e => e.FTitle.Contains(request.Keyword));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new ExceptionListDto
            {
                Id = e.FID,
                ExceptionNo = e.FExceptionNo,
                Title = e.FTitle,
                Type = e.FType,
                Status = e.FStatus,
                Priority = e.FPriority,
                Source = e.FSource,
                Deadline = e.FDeadline,
                CreateTime = e.FCreateTime,
            })
            .ToListAsync();

        return ApiResult<PagedResult<ExceptionListDto>>.Success(new PagedResult<ExceptionListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
        });
    }

    public async Task<ApiResult<ExceptionDetailDto>> GetDetailAsync(long orgId, long id)
    {
        var entity = await _db.Set<QlException>().FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId);
        if (entity == null)
            return ApiResult<ExceptionDetailDto>.Fail("异常单不存在");

        var logs = await _db.Set<QlExceptionLog>()
            .Where(l => l.FExceptionId == id)
            .OrderByDescending(l => l.FCreateTime)
            .Select(l => new ExceptionLogDto
            {
                Id = l.FID,
                Action = l.FAction,
                Remark = l.FRemark,
                FromStatus = l.FFromStatus,
                ToStatus = l.FToStatus,
                CreateTime = l.FCreateTime,
            })
            .ToListAsync();

        var dto = new ExceptionDetailDto
        {
            Id = entity.FID,
            ExceptionNo = entity.FExceptionNo,
            Title = entity.FTitle,
            Description = entity.FDescription,
            Type = entity.FType,
            Status = entity.FStatus,
            Priority = entity.FPriority,
            Source = entity.FSource,
            RelatedModule = entity.FRelatedModule,
            RelatedEntityId = entity.FRelatedEntityId,
            AssigneeId = entity.FAssigneeId,
            DispatchMethod = entity.FDispatchMethod,
            DispatchEntityId = entity.FDispatchEntityId,
            Deadline = entity.FDeadline,
            ClosedTime = entity.FClosedTime,
            CreatorId = entity.FCreatorId,
            CreateTime = entity.FCreateTime,
            UpdateTime = entity.FUpdateTime,
            Logs = logs,
        };

        return ApiResult<ExceptionDetailDto>.Success(dto);
    }

    public async Task<ApiResult<ExceptionListDto>> CreateAsync(long orgId, long operatorId, CreateExceptionRequest request)
    {
        // 生成异常编号 EX-yyyyMMddNNN
        var today = DateTime.Now.Date;
        var prefix = $"EX-{today:yyyyMMdd}";
        var maxNo = await _db.Set<QlException>()
            .Where(e => e.FOrgId == orgId && e.FExceptionNo.StartsWith(prefix))
            .OrderByDescending(e => e.FExceptionNo)
            .Select(e => e.FExceptionNo)
            .FirstOrDefaultAsync();

        int seq = 1;
        if (maxNo != null && maxNo.Length >= 14 && int.TryParse(maxNo.Substring(11), out var lastSeq))
            seq = lastSeq + 1;
        var exceptionNo = $"{prefix}{seq:D3}";

        var entity = new QlException
        {
            FOrgId = orgId,
            FExceptionNo = exceptionNo,
            FTitle = request.Title,
            FDescription = request.Description,
            FType = request.Type,
            FStatus = (int)ExceptionStatus.Pending,
            FPriority = request.Priority,
            FSource = request.Source,
            FRelatedModule = request.RelatedModule,
            FRelatedEntityId = request.RelatedEntityId,
            FDeadline = request.Deadline,
            FCreatorId = operatorId,
        };

        _db.Set<QlException>().Add(entity);
        await _db.SaveChangesAsync();

        // 记录创建日志
        _db.Set<QlExceptionLog>().Add(new QlExceptionLog
        {
            FExceptionId = entity.FID,
            FOperatorId = operatorId,
            FAction = "创建",
            FRemark = $"创建异常单 {exceptionNo}",
            FToStatus = (int)ExceptionStatus.Pending,
        });
        await _db.SaveChangesAsync();

        // 发布异常创建事件
        try
        {
            await _eventDispatcher.PublishAsync(new ExceptionCreatedEvent
            {
                ExceptionId = entity.FID,
                Title = entity.FTitle ?? request.Title,
                Priority = request.Priority switch { 3 => "urgent", 2 => "high", 1 => "normal", _ => "low" },
                CreatorId = operatorId,
                OrgId = orgId,
                TriggeredByUserId = operatorId,
                ModuleCode = "quality"
            });
        }
        catch (global::System.Exception ex)
        {
            _logger.LogWarning(ex, "发布 ExceptionCreatedEvent 失败，ExceptionId={ExceptionId}", entity.FID);
        }

        return ApiResult<ExceptionListDto>.Success(new ExceptionListDto
        {
            Id = entity.FID,
            ExceptionNo = entity.FExceptionNo,
            Title = entity.FTitle ?? string.Empty,
            Type = entity.FType,
            Status = entity.FStatus,
            Priority = entity.FPriority,
            Source = entity.FSource,
            Deadline = entity.FDeadline,
            CreateTime = entity.FCreateTime,
        });
    }

    public async Task<ApiResult<ExceptionListDto>> UpdateAsync(long orgId, long operatorId, long id, UpdateExceptionRequest request)
    {
        var entity = await _db.Set<QlException>().FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId);
        if (entity == null)
            return ApiResult<ExceptionListDto>.Fail("异常单不存在");

        if (request.Title != null) entity.FTitle = request.Title;
        if (request.Description != null) entity.FDescription = request.Description;
        if (request.Priority.HasValue) entity.FPriority = request.Priority.Value;
        if (request.Deadline.HasValue) entity.FDeadline = request.Deadline.Value;
        entity.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();

        return ApiResult<ExceptionListDto>.Success(new ExceptionListDto
        {
            Id = entity.FID,
            Title = entity.FTitle,
            Type = entity.FType,
            Status = entity.FStatus,
            Priority = entity.FPriority,
            Source = entity.FSource,
            Deadline = entity.FDeadline,
            CreateTime = entity.FCreateTime,
        });
    }

    public async Task<ApiResult<bool>> DeleteAsync(long orgId, long operatorId, long id)
    {
        var entity = await _db.Set<QlException>().FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId);
        if (entity == null)
            return ApiResult<bool>.Fail("异常单不存在");

        _db.Set<QlException>().Remove(entity);
        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }

    public async Task<ApiResult<bool>> DispatchAsync(long orgId, long operatorId, long id, DispatchExceptionRequest request)
    {
        var entity = await _db.Set<QlException>().FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId);
        if (entity == null)
            return ApiResult<bool>.Fail("异常单不存在");

        var oldStatus = entity.FStatus;
        entity.FAssigneeId = request.AssigneeId;
        entity.FDispatchMethod = request.DispatchMethod;
        entity.FStatus = (int)ExceptionStatus.Processing;
        entity.FUpdateTime = DateTime.Now;

        // 计算截止时间
        var timeoutHours = request.TimeoutHours ?? 24;
        if (!request.TimeoutHours.HasValue && entity.FRuleId.HasValue)
        {
            // 查关联规则的默认超时（当前规则实体无超时字段，用默认24h）
            timeoutHours = 24;
        }
        entity.FDeadline = DateTime.Now.AddHours(timeoutHours);

        // 生成派发日志备注
        var dispatchRemark = request.DispatchMethod switch
        {
            0 => $"已通过OA流程派发给[用户{request.AssigneeId}]",
            1 => $"已创建工作任务并分配给[用户{request.AssigneeId}]",
            2 => $"已发送消息预警通知[用户{request.AssigneeId}]",
            _ => $"已派发给[用户{request.AssigneeId}]"
        };
        if (!string.IsNullOrWhiteSpace(request.Remark))
            dispatchRemark += $"，备注：{request.Remark}";

        _db.Set<QlExceptionLog>().Add(new QlExceptionLog
        {
            FExceptionId = id,
            FOperatorId = operatorId,
            FAction = "派发",
            FRemark = dispatchRemark,
            FFromStatus = oldStatus,
            FToStatus = (int)ExceptionStatus.Processing,
        });

        await _db.SaveChangesAsync();

        // 发布异常派发事件
        try
        {
            await _eventDispatcher.PublishAsync(new ExceptionDispatchedEvent
            {
                ExceptionId = id,
                AssigneeId = request.AssigneeId,
                DispatchMethod = request.DispatchMethod switch { 0 => "oa", 1 => "task", 2 => "alert", _ => "other" },
                Title = entity.FTitle ?? "",
                OrgId = orgId,
                TriggeredByUserId = operatorId,
                ModuleCode = "quality"
            });
        }
        catch (global::System.Exception ex)
        {
            _logger.LogWarning(ex, "发布 ExceptionDispatchedEvent 失败，ExceptionId={ExceptionId}", id);
        }

        return ApiResult<bool>.Success(true);
    }

    public async Task<ApiResult<bool>> CloseAsync(long orgId, long operatorId, long id, CloseExceptionRequest request)
    {
        var entity = await _db.Set<QlException>().FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId);
        if (entity == null)
            return ApiResult<bool>.Fail("异常单不存在");

        var oldStatus = entity.FStatus;
        entity.FStatus = (int)ExceptionStatus.Closed;
        entity.FClosedTime = DateTime.Now;
        entity.FUpdateTime = DateTime.Now;

        var closeRemark = "关闭异常单";
        if (!string.IsNullOrWhiteSpace(request.Result))
            closeRemark += $"，处理结果：{request.Result}";
        if (!string.IsNullOrWhiteSpace(request.Remark))
            closeRemark += $"，备注：{request.Remark}";

        _db.Set<QlExceptionLog>().Add(new QlExceptionLog
        {
            FExceptionId = id,
            FOperatorId = operatorId,
            FAction = "关闭",
            FRemark = closeRemark,
            FFromStatus = oldStatus,
            FToStatus = (int)ExceptionStatus.Closed,
        });

        await _db.SaveChangesAsync();

        // 发布异常关闭事件
        try
        {
            await _eventDispatcher.PublishAsync(new ExceptionClosedEvent
            {
                ExceptionId = id,
                ClosedByUserId = operatorId,
                Resolution = request.Result ?? "",
                TriggeredByUserId = operatorId,
                ModuleCode = "quality"
            });
        }
        catch (global::System.Exception ex)
        {
            _logger.LogWarning(ex, "发布 ExceptionClosedEvent 失败，ExceptionId={ExceptionId}", id);
        }

        return ApiResult<bool>.Success(true);
    }

    public async Task<ApiResult<bool>> ReassignAsync(long orgId, long operatorId, long id, ReassignExceptionRequest request)
    {
        var entity = await _db.Set<QlException>().FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId);
        if (entity == null)
            return ApiResult<bool>.Fail("异常单不存在");

        if (entity.FStatus == (int)ExceptionStatus.Closed)
            return ApiResult<bool>.Fail("已关闭的异常单不能转派");

        var oldAssigneeId = entity.FAssigneeId;
        entity.FAssigneeId = request.NewAssigneeId;
        entity.FUpdateTime = DateTime.Now;

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "未说明" : request.Reason;
        _db.Set<QlExceptionLog>().Add(new QlExceptionLog
        {
            FExceptionId = id,
            FOperatorId = operatorId,
            FAction = "转派",
            FRemark = $"从[用户{oldAssigneeId}]转派给[用户{request.NewAssigneeId}]，原因：{reason}",
            FFromStatus = entity.FStatus,
            FToStatus = entity.FStatus,
        });

        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }

    public async Task<ApiResult<ExceptionCountByStatusDto>> GetCountByStatusAsync(long orgId)
    {
        var list = await _db.Set<QlException>()
            .Where(e => e.FOrgId == orgId)
            .GroupBy(e => e.FStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var dto = new ExceptionCountByStatusDto
        {
            Pending = list.FirstOrDefault(x => x.Status == (int)ExceptionStatus.Pending)?.Count ?? 0,
            Processing = list.FirstOrDefault(x => x.Status == (int)ExceptionStatus.Processing)?.Count ?? 0,
            Overdue = list.FirstOrDefault(x => x.Status == (int)ExceptionStatus.Overdue)?.Count ?? 0,
            Closed = list.FirstOrDefault(x => x.Status == (int)ExceptionStatus.Closed)?.Count ?? 0,
        };
        dto.Total = dto.Pending + dto.Processing + dto.Overdue + dto.Closed;

        return ApiResult<ExceptionCountByStatusDto>.Success(dto);
    }
}
