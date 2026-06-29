using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Services;

/// <summary>
/// 操作日志服务
/// </summary>
public class OperationLogService
{
    private readonly IRepository<FinOperationLog> _logRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<OperationLogService> _logger;

    public OperationLogService(IRepository<FinOperationLog> logRepository, IHttpContextAccessor httpContextAccessor, ILogger<OperationLogService> logger)
    {
        _logRepository = logRepository;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    /// <summary>
    /// 记录操作日志（fire-and-forget，不影响主业务流程）
    /// </summary>
    public async Task LogAsync(
        long accountSetId,
        string module,
        string operationType,
        string description,
        long? targetId = null,
        string? targetCode = null,
        long operatorId = 0,
        string operatorName = "",
        string? ipAddress = null,
        string? extraData = null)
    {
        try
        {
            var log = new FinOperationLog
            {
                FAccountSetId = accountSetId,
                FOrgId = GetCurrentOrgId(),
                FModule = module,
                FOperationType = operationType,
                FDescription = description,
                FTargetId = targetId,
                FTargetCode = targetCode,
                FOperatorId = operatorId,
                FOperatorName = operatorName,
                FOperationTime = DateTime.Now,
                FIpAddress = ipAddress,
                FExtraData = extraData,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };

            await _logRepository.AddAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "操作日志记录失败");
        }
    }

    /// <summary>
    /// 分页查询操作日志
    /// </summary>
    public async Task<PagedResult<OperationLogDto>> GetLogsAsync(OperationLogQueryRequest request)
    {
        var query = _logRepository.Query();

        if (request.AccountSetId > 0)
            query = query.Where(l => l.FAccountSetId == request.AccountSetId);

        if (!string.IsNullOrEmpty(request.Module))
            query = query.Where(l => l.FModule == request.Module);

        if (!string.IsNullOrEmpty(request.OperationType))
            query = query.Where(l => l.FOperationType == request.OperationType);

        if (request.StartDate.HasValue)
            query = query.Where(l => l.FOperationTime >= request.StartDate.Value.Date);

        if (request.EndDate.HasValue)
            query = query.Where(l => l.FOperationTime < request.EndDate.Value.Date.AddDays(1));

        var total = await query.CountAsync();

        var logs = await query
            .OrderByDescending(l => l.FOperationTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var items = logs.Select(l => new OperationLogDto
        {
            Id = l.FID,
            AccountSetId = l.FAccountSetId,
            Module = l.FModule,
            OperationType = l.FOperationType,
            Description = l.FDescription,
            TargetId = l.FTargetId,
            TargetCode = l.FTargetCode,
            OperatorId = l.FOperatorId,
            OperatorName = l.FOperatorName,
            OperationTime = l.FOperationTime,
            IpAddress = l.FIpAddress,
            ExtraData = l.FExtraData
        }).ToList();

        return new PagedResult<OperationLogDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }
}
