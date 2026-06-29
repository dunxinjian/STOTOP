using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Services;

/// <summary>
/// 字段级变更追踪服务：比较实体变更前后的属性差异，记录到变更历史表
/// </summary>
public class ChangeTrackingService
{
    private readonly IRepository<FinChangeHistory> _historyRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChangeTrackingService(
        IRepository<FinChangeHistory> historyRepository,
        IHttpContextAccessor httpContextAccessor)
    {
        _historyRepository = historyRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 通用变更追踪：比较两个实体对象的所有属性，记录差异
    /// </summary>
    public async Task TrackChangesAsync<T>(string entityType, long entityId, T oldEntity, T newEntity, long accountSetId = 0) where T : class
    {
        try
        {
            var operatorId = GetCurrentUserId();
            var operatorName = GetCurrentUserName();
            var now = DateTime.Now;

            var properties = typeof(T).GetProperties();
            var changes = new List<FinChangeHistory>();

            foreach (var prop in properties)
            {
                if (ShouldSkipProperty(prop.Name)) continue;

                // 跳过不可读的属性和集合导航属性
                if (!prop.CanRead) continue;
                if (prop.PropertyType.IsGenericType &&
                    typeof(global::System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType) &&
                    prop.PropertyType != typeof(string))
                    continue;

                var oldValue = prop.GetValue(oldEntity)?.ToString();
                var newValue = prop.GetValue(newEntity)?.ToString();

                if (oldValue != newValue)
                {
                    changes.Add(new FinChangeHistory
                    {
                        FEntityType = entityType,
                        FEntityId = entityId,
                        FFieldName = prop.Name,
                        FOldValue = oldValue,
                        FNewValue = newValue,
                        FOperatorId = operatorId,
                        FOperatorName = operatorName,
                        FOperationTime = now,
                        FAccountSetId = accountSetId
                    });
                }
            }

            if (changes.Count > 0)
            {
                foreach (var change in changes)
                {
                    await _historyRepository.AddAsync(change);
                }
            }
        }
        catch
        {
            // 变更追踪失败不应阻塞业务操作
        }
    }

    /// <summary>
    /// 记录单个字段变更
    /// </summary>
    public async Task TrackFieldChangeAsync(string entityType, long entityId, string fieldName, string? oldValue, string? newValue, long accountSetId = 0)
    {
        try
        {
            if (oldValue == newValue) return;

            var now = DateTime.Now;
            var change = new FinChangeHistory
            {
                FEntityType = entityType,
                FEntityId = entityId,
                FFieldName = fieldName,
                FOldValue = oldValue,
                FNewValue = newValue,
                FOperatorId = GetCurrentUserId(),
                FOperatorName = GetCurrentUserName(),
                FOperationTime = now,
                FAccountSetId = accountSetId
            };

            await _historyRepository.AddAsync(change);
        }
        catch
        {
            // 变更追踪失败不应阻塞业务操作
        }
    }

    /// <summary>
    /// 查询变更历史
    /// </summary>
    public async Task<List<FinChangeHistory>> GetChangeHistoryAsync(string entityType, long entityId)
    {
        return await _historyRepository.Query()
            .Where(h => h.FEntityType == entityType && h.FEntityId == entityId)
            .OrderByDescending(h => h.FOperationTime)
            .ToListAsync();
    }

    private static bool ShouldSkipProperty(string propertyName)
    {
        return propertyName is "FID" or "FCreatedTime" or "FUpdatedTime" or "FOrgId";
    }

    private long GetCurrentUserId()
    {
        var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (claim != null && long.TryParse(claim.Value, out var userId))
            return userId;
        return 0;
    }

    private string GetCurrentUserName()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value ?? "";
    }
}
