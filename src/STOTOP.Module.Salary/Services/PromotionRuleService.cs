using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Salary.Dtos;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Services;

public class PromotionRuleService : IPromotionRuleService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<PromotionRuleService> _logger;

    public PromotionRuleService(STOTOPDbContext context, ILogger<PromotionRuleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResult<List<PromotionRuleDto>>> GetListAsync(long orgId)
    {
        var list = await _context.Set<PromotionRule>()
            .Where(r => r.FOrgId == orgId)
            .OrderByDescending(r => r.FID)
            .ToListAsync();

        var gradeIds = list.SelectMany(r => new[] { r.F当前档位ID, r.F目标档位ID }).Distinct().ToList();
        var gradeMap = await _context.Set<SalaryGrade>()
            .Where(g => gradeIds.Contains(g.FID))
            .ToDictionaryAsync(g => g.FID, g => g.F档位名称);

        var dtos = list.Select(r => MapToDto(r, gradeMap)).ToList();
        return ApiResult<List<PromotionRuleDto>>.Success(dtos);
    }

    public async Task<ApiResult<PromotionRuleDto>> CreateAsync(long orgId, CreatePromotionRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RuleName))
            return ApiResult<PromotionRuleDto>.Fail("规则名称不能为空");
        if (request.CurrentGradeId == request.TargetGradeId)
            return ApiResult<PromotionRuleDto>.Fail("当前档位与目标档位不能相同");
        if (request.AScoreThreshold <= 0)
            return ApiResult<PromotionRuleDto>.Fail("A分阈值必须大于0");

        var entity = new PromotionRule
        {
            FOrgId = orgId,
            F规则名称 = request.RuleName.Trim(),
            F当前档位ID = request.CurrentGradeId,
            F目标档位ID = request.TargetGradeId,
            FA分阈值 = request.AScoreThreshold,
            F附加条件JSON = request.ExtraConditionJson,
            F启用状态 = request.IsEnabled,
            F创建时间 = DateTime.Now,
            F更新时间 = DateTime.Now
        };

        _context.Set<PromotionRule>().Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[Salary] 创建晋升规则 Id={Id} Name={Name} OrgId={OrgId}", entity.FID, entity.F规则名称, orgId);
        return ApiResult<PromotionRuleDto>.Success(MapToDto(entity, new Dictionary<long, string>()));
    }

    public async Task<ApiResult<PromotionRuleDto>> UpdateAsync(long orgId, long id, UpdatePromotionRuleRequest request)
    {
        var entity = await _context.Set<PromotionRule>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult<PromotionRuleDto>.Fail("规则不存在");

        entity.F规则名称 = request.RuleName.Trim();
        entity.F当前档位ID = request.CurrentGradeId;
        entity.F目标档位ID = request.TargetGradeId;
        entity.FA分阈值 = request.AScoreThreshold;
        entity.F附加条件JSON = request.ExtraConditionJson;
        entity.F启用状态 = request.IsEnabled;
        entity.F更新时间 = DateTime.Now;

        await _context.SaveChangesAsync();
        return ApiResult<PromotionRuleDto>.Success(MapToDto(entity, new Dictionary<long, string>()));
    }

    public async Task<ApiResult> EnableAsync(long orgId, long id)
    {
        var entity = await _context.Set<PromotionRule>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult.Fail("规则不存在");

        entity.F启用状态 = !entity.F启用状态;
        entity.F更新时间 = DateTime.Now;
        await _context.SaveChangesAsync();

        return ApiResult.Ok(entity.F启用状态 ? "已启用" : "已禁用");
    }

    private static PromotionRuleDto MapToDto(PromotionRule r, Dictionary<long, string> gradeMap) => new()
    {
        Id = r.FID,
        OrgId = r.FOrgId,
        RuleName = r.F规则名称,
        CurrentGradeId = r.F当前档位ID,
        CurrentGradeName = gradeMap.GetValueOrDefault(r.F当前档位ID),
        TargetGradeId = r.F目标档位ID,
        TargetGradeName = gradeMap.GetValueOrDefault(r.F目标档位ID),
        AScoreThreshold = r.FA分阈值,
        ExtraConditionJson = r.F附加条件JSON,
        IsEnabled = r.F启用状态,
        CreateTime = r.F创建时间,
        UpdateTime = r.F更新时间
    };
}
