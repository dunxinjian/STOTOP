using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Points.Constants;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Services;

public class PointRuleService : IPointRuleService
{
    private readonly STOTOPDbContext _db;

    public PointRuleService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<PointRuleListDto>>> GetPagedListAsync(long orgId, PointRulePagedRequest request)
    {
        var query = _db.Set<PmPointRule>()
            .Where(r => r.FOrgId == orgId)
            .AsQueryable();

        if (request.SourceId.HasValue)
            query = query.Where(r => r.FSourceId == request.SourceId.Value);

        if (request.IsEnabled.HasValue)
            query = query.Where(r => r.FIsEnabled == request.IsEnabled.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(r => r.FRuleName.Contains(kw) || r.FRuleCode.Contains(kw));
        }

        var total = await query.CountAsync();

        var rules = await query
            .OrderBy(r => r.FSortOrder)
            .ThenByDescending(r => r.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // 获取来源名称
        var sourceIds = rules.Select(r => r.FSourceId).Distinct().ToList();
        var sources = await _db.Set<PmPointSource>()
            .Where(s => sourceIds.Contains(s.FID))
            .Select(s => new { s.FID, s.FSourceName })
            .ToListAsync();
        var sourceDict = sources.ToDictionary(s => s.FID, s => s.FSourceName);

        var items = rules.Select(r => new PointRuleListDto
        {
            Id = r.FID,
            OrgId = r.FOrgId,
            SourceId = r.FSourceId,
            SourceName = sourceDict.GetValueOrDefault(r.FSourceId),
            RuleName = r.FRuleName,
            RuleCode = r.FRuleCode,
            EventType = r.FEventType,
            PointValue = r.FPointValue,
            ConditionDescription = r.FConditionDescription,
            CycleLimit = r.FCycleLimit,
            RequireApproval = r.FRequireApproval,
            SortOrder = r.FSortOrder,
            IsEnabled = r.FIsEnabled,
            CreateTime = r.FCreateTime,
            UpdateTime = r.FUpdateTime
        }).ToList();

        return ApiResult<PagedResult<PointRuleListDto>>.Success(new PagedResult<PointRuleListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<PointRuleDetailDto>> GetByIdAsync(long id)
    {
        var rule = await _db.Set<PmPointRule>().FirstOrDefaultAsync(r => r.FID == id);
        if (rule == null)
            return ApiResult<PointRuleDetailDto>.Fail("积分规则不存在");

        var dto = MapToDetailDto(rule);

        var source = await _db.Set<PmPointSource>()
            .Where(s => s.FID == rule.FSourceId)
            .Select(s => new { s.FSourceName })
            .FirstOrDefaultAsync();
        dto.SourceName = source?.FSourceName;

        return ApiResult<PointRuleDetailDto>.Success(dto);
    }

    public async Task<ApiResult<PointRuleDetailDto>> CreateAsync(long orgId, CreatePointRuleRequest request)
    {
        // 验证来源存在
        var sourceExists = await _db.Set<PmPointSource>().AnyAsync(s => s.FID == request.SourceId && s.FOrgId == orgId);
        if (!sourceExists)
            return ApiResult<PointRuleDetailDto>.Fail("积分来源不存在");

        // 验证规则编码唯一
        var codeExists = await _db.Set<PmPointRule>()
            .AnyAsync(r => r.FOrgId == orgId && r.FRuleCode == request.RuleCode);
        if (codeExists)
            return ApiResult<PointRuleDetailDto>.Fail("规则编码已存在");

        var entity = new PmPointRule
        {
            FOrgId = orgId,
            FSourceId = request.SourceId,
            FRuleName = request.RuleName,
            FRuleCode = request.RuleCode,
            FEventType = request.EventType,
            FPointValue = request.PointValue,
            FConditionExpression = request.ConditionExpression,
            FConditionDescription = request.ConditionDescription,
            FMultiplierRule = request.MultiplierRule,
            FCycleLimit = request.CycleLimit,
            FRequireApproval = request.RequireApproval,
            FSortOrder = request.SortOrder,
            FIsEnabled = true,
            F账户类型 = request.AccountType <= 0 ? PointAccountTypes.B : request.AccountType,
            F清算策略 = request.ResetStrategy,
            F转换比例 = request.ConvertRatio <= 0 ? 1.0m : request.ConvertRatio,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<PmPointRule>().Add(entity);
        await _db.SaveChangesAsync();

        return ApiResult<PointRuleDetailDto>.Success(MapToDetailDto(entity));
    }

    public async Task<ApiResult<PointRuleDetailDto>> UpdateAsync(long id, UpdatePointRuleRequest request)
    {
        var entity = await _db.Set<PmPointRule>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (entity == null)
            return ApiResult<PointRuleDetailDto>.Fail("积分规则不存在");

        entity.FSourceId = request.SourceId;
        entity.FRuleName = request.RuleName;
        entity.FEventType = request.EventType;
        entity.FPointValue = request.PointValue;
        entity.FConditionExpression = request.ConditionExpression;
        entity.FConditionDescription = request.ConditionDescription;
        entity.FMultiplierRule = request.MultiplierRule;
        entity.FCycleLimit = request.CycleLimit;
        entity.FRequireApproval = request.RequireApproval;
        entity.FSortOrder = request.SortOrder;
        entity.FIsEnabled = request.IsEnabled;
        entity.F账户类型 = request.AccountType <= 0 ? PointAccountTypes.B : request.AccountType;
        entity.F清算策略 = request.ResetStrategy;
        entity.F转换比例 = request.ConvertRatio <= 0 ? 1.0m : request.ConvertRatio;
        entity.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return ApiResult<PointRuleDetailDto>.Success(MapToDetailDto(entity));
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var entity = await _db.Set<PmPointRule>().FirstOrDefaultAsync(r => r.FID == id);
        if (entity == null)
            return ApiResult<bool>.Fail("积分规则不存在");

        // 检查是否有关联的积分记录
        var hasRecords = await _db.Set<PmPointRecord>().AnyAsync(r => r.FRuleId == id);
        if (hasRecords)
            return ApiResult<bool>.Fail("该规则已有关联的积分记录，无法删除");

        _db.Set<PmPointRule>().Remove(entity);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "删除成功");
    }

    public async Task<ApiResult<bool>> ToggleAsync(long id)
    {
        var entity = await _db.Set<PmPointRule>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (entity == null)
            return ApiResult<bool>.Fail("积分规则不存在");

        entity.FIsEnabled = !entity.FIsEnabled;
        entity.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, entity.FIsEnabled ? "已启用" : "已禁用");
    }

    public async Task<List<PointRuleDetailDto>> MatchRulesAsync(long orgId, string eventType, object? context)
    {
        var rules = await _db.Set<PmPointRule>()
            .Where(r => r.FOrgId == orgId && r.FEventType == eventType && r.FIsEnabled)
            .OrderBy(r => r.FSortOrder)
            .ToListAsync();

        var matched = new List<PointRuleDetailDto>();

        foreach (var rule in rules)
        {
            // 解析条件表达式JSON，判断是否匹配
            if (!string.IsNullOrWhiteSpace(rule.FConditionExpression))
            {
                if (!EvaluateCondition(rule.FConditionExpression, context))
                    continue;
            }

            var dto = MapToDetailDto(rule);

            // 计算倍率
            if (!string.IsNullOrWhiteSpace(rule.FMultiplierRule))
            {
                dto.PointValue = ApplyMultiplier(rule.FPointValue, rule.FMultiplierRule, context);
            }

            matched.Add(dto);
        }

        return matched;
    }

    /// <summary>
    /// 评估条件表达式（JSON格式），判断当前上下文是否满足规则条件
    /// </summary>
    private static bool EvaluateCondition(string conditionExpression, object? context)
    {
        if (context == null) return false;

        try
        {
            var conditions = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(conditionExpression);
            if (conditions == null) return true;

            // 将上下文转为字典进行比较
            var contextJson = JsonSerializer.Serialize(context);
            var contextDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(contextJson);
            if (contextDict == null) return false;

            foreach (var (key, expectedValue) in conditions)
            {
                if (!contextDict.TryGetValue(key, out var actualValue))
                    return false;

                if (actualValue.ToString() != expectedValue.ToString())
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 应用倍率规则，计算最终积分值
    /// </summary>
    private static int ApplyMultiplier(int baseValue, string multiplierRule, object? context)
    {
        try
        {
            var rules = JsonSerializer.Deserialize<Dictionary<string, double>>(multiplierRule);
            if (rules == null || context == null) return baseValue;

            var contextJson = JsonSerializer.Serialize(context);
            var contextDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(contextJson);
            if (contextDict == null) return baseValue;

            double multiplier = 1.0;

            // 遍历倍率规则，匹配上下文中的字段值，找到对应的倍率
            foreach (var (key, value) in rules)
            {
                // 倍率规则格式：{ "field.value": multiplier }
                // 例如 { "priority.high": 2.0, "priority.urgent": 3.0 }
                var parts = key.Split('.', 2);
                if (parts.Length == 2 && contextDict.TryGetValue(parts[0], out var contextValue))
                {
                    if (contextValue.ToString() == parts[1])
                    {
                        multiplier = value;
                        break;
                    }
                }
            }

            return (int)Math.Round(baseValue * multiplier);
        }
        catch
        {
            return baseValue;
        }
    }

    private static PointRuleDetailDto MapToDetailDto(PmPointRule r) => new()
    {
        Id = r.FID,
        OrgId = r.FOrgId,
        SourceId = r.FSourceId,
        RuleName = r.FRuleName,
        RuleCode = r.FRuleCode,
        EventType = r.FEventType,
        PointValue = r.FPointValue,
        ConditionExpression = r.FConditionExpression,
        ConditionDescription = r.FConditionDescription,
        MultiplierRule = r.FMultiplierRule,
        CycleLimit = r.FCycleLimit,
        RequireApproval = r.FRequireApproval,
        SortOrder = r.FSortOrder,
        IsEnabled = r.FIsEnabled,
        AccountType = r.F账户类型,
        ResetStrategy = r.F清算策略,
        ConvertRatio = r.F转换比例,
        CreateTime = r.FCreateTime,
        UpdateTime = r.FUpdateTime
    };
}
