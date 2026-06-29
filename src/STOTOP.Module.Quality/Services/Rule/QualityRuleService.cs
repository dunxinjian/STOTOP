using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Services.Rule;

public class QualityRuleService : IQualityRuleService
{
    private readonly STOTOPDbContext _db;

    public QualityRuleService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<QualityRuleDto>>> GetPagedAsync(long orgId, RulePagedRequest request)
    {
        var query = _db.Set<QlRule>().Where(r => r.FOrgId == orgId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(r => r.FRuleName.Contains(request.Keyword));
        if (!string.IsNullOrWhiteSpace(request.BusinessLine))
            query = query.Where(r => r.FBusinessLine == request.BusinessLine);
        if (request.Status.HasValue)
            query = query.Where(r => r.FStatus == request.Status.Value);

        var total = await query.CountAsync();

        var conditionCounts = _db.Set<QlRuleCondition>()
            .GroupBy(c => c.FRuleId)
            .Select(g => new { RuleId = g.Key, Count = g.Count() });

        var items = await query
            .OrderByDescending(r => r.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .GroupJoin(conditionCounts, r => r.FID, cc => cc.RuleId, (r, ccs) => new { Rule = r, Counts = ccs })
            .SelectMany(x => x.Counts.DefaultIfEmpty(), (x, cc) => new QualityRuleDto
            {
                Id = x.Rule.FID,
                Name = x.Rule.FRuleName,
                BusinessLine = x.Rule.FBusinessLine,
                ConditionExpression = x.Rule.FConditionExpression,
                DispatchMethod = x.Rule.FDispatchMethod,
                DispatchTarget = x.Rule.FDispatchTarget,
                DefaultPriority = x.Rule.FDefaultPriority,
                TimeoutHours = x.Rule.FTimeoutHours,
                Status = x.Rule.FStatus,
                Description = x.Rule.FDescription,
                ConditionCount = cc != null ? cc.Count : 0,
                CreatedTime = x.Rule.FCreatedTime,
                UpdatedTime = x.Rule.FUpdatedTime,
            })
            .ToListAsync();

        return ApiResult<PagedResult<QualityRuleDto>>.Success(new PagedResult<QualityRuleDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
        });
    }

    public async Task<ApiResult<QualityRuleDetailDto>> GetByIdAsync(long orgId, long id)
    {
        var entity = await _db.Set<QlRule>().FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult<QualityRuleDetailDto>.Fail("规则不存在");

        var conditions = await _db.Set<QlRuleCondition>()
            .Where(c => c.FRuleId == id)
            .OrderBy(c => c.FSort)
            .Select(c => new RuleConditionDto
            {
                Id = c.FID,
                FieldName = c.FFieldName,
                Operator = c.FOperator,
                Threshold = c.FThreshold,
                LogicRelation = c.FLogicRelation,
                Sort = c.FSort,
            })
            .ToListAsync();

        return ApiResult<QualityRuleDetailDto>.Success(new QualityRuleDetailDto
        {
            Id = entity.FID,
            Name = entity.FRuleName,
            BusinessLine = entity.FBusinessLine,
            ConditionExpression = entity.FConditionExpression,
            DispatchMethod = entity.FDispatchMethod,
            DispatchTarget = entity.FDispatchTarget,
            DefaultPriority = entity.FDefaultPriority,
            TimeoutHours = entity.FTimeoutHours,
            Status = entity.FStatus,
            Description = entity.FDescription,
            ConditionCount = conditions.Count,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime,
            Conditions = conditions,
        });
    }

    public async Task<ApiResult<QualityRuleDto>> CreateAsync(long orgId, long userId, CreateRuleRequest request)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var entity = new QlRule
            {
                FOrgId = orgId,
                FRuleName = request.Name,
                FBusinessLine = request.BusinessLine,
                FDispatchMethod = request.DispatchMethod,
                FDispatchTarget = request.DispatchTarget,
                FDefaultPriority = request.DefaultPriority,
                FTimeoutHours = request.TimeoutHours,
                FDescription = request.Description,
                FCreatorId = userId,
                FStatus = 1,
                FCreatedTime = DateTime.Now,
            };

            _db.Set<QlRule>().Add(entity);
            await _db.SaveChangesAsync();

            if (request.Conditions.Any())
            {
                var conditionExpParts = new List<string>();
                foreach (var c in request.Conditions)
                {
                    _db.Set<QlRuleCondition>().Add(new QlRuleCondition
                    {
                        FRuleId = entity.FID,
                        FFieldName = c.FieldName,
                        FOperator = c.Operator,
                        FThreshold = c.Threshold,
                        FLogicRelation = c.LogicRelation,
                        FSort = c.Sort,
                    });
                    conditionExpParts.Add($"{c.FieldName} {c.Operator} {c.Threshold}");
                }

                // 自动生成条件表达式
                entity.FConditionExpression = string.Join(" AND ", conditionExpParts);
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return ApiResult<QualityRuleDto>.Success(MapToDto(entity, request.Conditions.Count));
        }
        catch (global::System.Exception ex)
        {
            await transaction.RollbackAsync();
            return ApiResult<QualityRuleDto>.Fail($"创建规则失败: {ex.Message}");
        }
    }

    public async Task<ApiResult<QualityRuleDto>> UpdateAsync(long orgId, long userId, long id, UpdateRuleRequest request)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var entity = await _db.Set<QlRule>().AsTracking()
                .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
            if (entity == null)
                return ApiResult<QualityRuleDto>.Fail("规则不存在");

            entity.FRuleName = request.Name;
            entity.FBusinessLine = request.BusinessLine;
            entity.FDispatchMethod = request.DispatchMethod;
            entity.FDispatchTarget = request.DispatchTarget;
            entity.FDefaultPriority = request.DefaultPriority;
            entity.FTimeoutHours = request.TimeoutHours;
            entity.FDescription = request.Description;
            entity.FUpdatedTime = DateTime.Now;

            // 删除旧条件
            var oldConditions = await _db.Set<QlRuleCondition>()
                .Where(c => c.FRuleId == id).ToListAsync();
            _db.Set<QlRuleCondition>().RemoveRange(oldConditions);

            // 重新创建条件
            if (request.Conditions.Any())
            {
                var conditionExpParts = new List<string>();
                foreach (var c in request.Conditions)
                {
                    _db.Set<QlRuleCondition>().Add(new QlRuleCondition
                    {
                        FRuleId = entity.FID,
                        FFieldName = c.FieldName,
                        FOperator = c.Operator,
                        FThreshold = c.Threshold,
                        FLogicRelation = c.LogicRelation,
                        FSort = c.Sort,
                    });
                    conditionExpParts.Add($"{c.FieldName} {c.Operator} {c.Threshold}");
                }
                entity.FConditionExpression = string.Join(" AND ", conditionExpParts);
            }
            else
            {
                entity.FConditionExpression = null;
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return ApiResult<QualityRuleDto>.Success(MapToDto(entity, request.Conditions.Count));
        }
        catch (global::System.Exception ex)
        {
            await transaction.RollbackAsync();
            return ApiResult<QualityRuleDto>.Fail($"更新规则失败: {ex.Message}");
        }
    }

    public async Task<ApiResult<bool>> DeleteAsync(long orgId, long id)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var entity = await _db.Set<QlRule>().AsTracking()
                .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
            if (entity == null)
                return ApiResult<bool>.Fail("规则不存在");

            // 先删条件
            var conditions = await _db.Set<QlRuleCondition>()
                .Where(c => c.FRuleId == id).ToListAsync();
            _db.Set<QlRuleCondition>().RemoveRange(conditions);

            // 再删规则
            _db.Set<QlRule>().Remove(entity);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return ApiResult<bool>.Success(true);
        }
        catch (global::System.Exception ex)
        {
            await transaction.RollbackAsync();
            return ApiResult<bool>.Fail($"删除规则失败: {ex.Message}");
        }
    }

    public async Task<ApiResult<bool>> ToggleAsync(long orgId, long id)
    {
        var entity = await _db.Set<QlRule>().AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult<bool>.Fail("规则不存在");

        entity.FStatus = entity.FStatus == 1 ? 0 : 1;
        entity.FUpdatedTime = DateTime.Now;
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true);
    }

    private static QualityRuleDto MapToDto(QlRule entity, int conditionCount)
    {
        return new QualityRuleDto
        {
            Id = entity.FID,
            Name = entity.FRuleName,
            BusinessLine = entity.FBusinessLine,
            ConditionExpression = entity.FConditionExpression,
            DispatchMethod = entity.FDispatchMethod,
            DispatchTarget = entity.FDispatchTarget,
            DefaultPriority = entity.FDefaultPriority,
            TimeoutHours = entity.FTimeoutHours,
            Status = entity.FStatus,
            Description = entity.FDescription,
            ConditionCount = conditionCount,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime,
        };
    }
}
