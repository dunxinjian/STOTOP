using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Salary.Dtos;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Services;

public class PromotionReviewService : IPromotionReviewService
{
    private readonly STOTOPDbContext _context;
    private readonly ISalaryArchiveService _archiveService;
    private readonly ILogger<PromotionReviewService> _logger;

    public PromotionReviewService(
        STOTOPDbContext context,
        ISalaryArchiveService archiveService,
        ILogger<PromotionReviewService> logger)
    {
        _context = context;
        _archiveService = archiveService;
        _logger = logger;
    }

    public async Task<ApiResult<List<PromotionReviewDto>>> GetListAsync(long orgId)
    {
        var list = await _context.Set<PromotionReview>()
            .Where(r => r.FOrgId == orgId)
            .OrderByDescending(r => r.FID)
            .ToListAsync();

        var gradeIds = list.SelectMany(r => new[] { r.F当前档位ID, r.F目标档位ID }).Distinct().ToList();
        var gradeMap = await _context.Set<SalaryGrade>()
            .Where(g => gradeIds.Contains(g.FID))
            .ToDictionaryAsync(g => g.FID, g => g.F档位名称);

        var ruleIds = list.Select(r => r.F规则ID).Distinct().ToList();
        var ruleMap = await _context.Set<PromotionRule>()
            .Where(r => ruleIds.Contains(r.FID))
            .ToDictionaryAsync(r => r.FID, r => r.F规则名称);

        var dtos = list.Select(r => MapToDto(r, gradeMap, ruleMap)).ToList();
        return ApiResult<List<PromotionReviewDto>>.Success(dtos);
    }

    public async Task<ApiResult<List<PromotionReviewDto>>> GetPendingListAsync(long orgId)
    {
        var list = await _context.Set<PromotionReview>()
            .Where(r => r.FOrgId == orgId && r.F状态 == 0)
            .OrderByDescending(r => r.F触发时间)
            .ToListAsync();

        var gradeIds = list.SelectMany(r => new[] { r.F当前档位ID, r.F目标档位ID }).Distinct().ToList();
        var gradeMap = await _context.Set<SalaryGrade>()
            .Where(g => gradeIds.Contains(g.FID))
            .ToDictionaryAsync(g => g.FID, g => g.F档位名称);

        var ruleIds = list.Select(r => r.F规则ID).Distinct().ToList();
        var ruleMap = await _context.Set<PromotionRule>()
            .Where(r => ruleIds.Contains(r.FID))
            .ToDictionaryAsync(r => r.FID, r => r.F规则名称);

        var dtos = list.Select(r => MapToDto(r, gradeMap, ruleMap)).ToList();
        return ApiResult<List<PromotionReviewDto>>.Success(dtos);
    }

    public async Task<ApiResult<PromotionReviewDto>> CreateAsync(long orgId, CreatePromotionReviewRequest request)
    {
        if (request.EmployeeId <= 0)
            return ApiResult<PromotionReviewDto>.Fail("员工ID无效");
        if (request.RuleId <= 0)
            return ApiResult<PromotionReviewDto>.Fail("规则ID无效");

        var entity = new PromotionReview
        {
            FOrgId = orgId,
            F员工ID = request.EmployeeId,
            F规则ID = request.RuleId,
            F当前档位ID = request.CurrentGradeId,
            F目标档位ID = request.TargetGradeId,
            F触发时间 = DateTime.Now,
            FA分快照 = request.AScoreSnapshot,
            F状态 = 0,
            F创建时间 = DateTime.Now
        };

        _context.Set<PromotionReview>().Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[Salary] 创建晋升评审 Id={Id} EmployeeId={EmpId} OrgId={OrgId}", entity.FID, entity.F员工ID, orgId);
        return ApiResult<PromotionReviewDto>.Success(MapToDto(entity, new Dictionary<long, string>(), new Dictionary<long, string>()));
    }

    public async Task<ApiResult> ApproveAsync(long orgId, long id, long reviewerId, ReviewPromotionRequest request)
    {
        var entity = await _context.Set<PromotionReview>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult.Fail("评审记录不存在");
        if (entity.F状态 != 0)
            return ApiResult.Fail("只有待评审状态才能审批");

        entity.F状态 = 1;
        entity.F评审人ID = reviewerId;
        entity.F评审时间 = DateTime.Now;
        entity.F评审意见 = request.Comment;
        entity.F生效日期 = request.EffectiveDate ?? DateTime.Today;

        await _context.SaveChangesAsync();

        // 获取原档案信息用于写入历史
        var archive = await _context.Set<SalaryArchive>()
            .FirstOrDefaultAsync(a => a.FOrgId == orgId && a.F员工ID == entity.F员工ID);
        var oldBaseSalary = archive?.F基本工资 ?? 0;

        // 调档：更新员工薪酬档案
        var adjustResult = await _archiveService.AdjustGradeAsync(orgId, entity.F员工ID, entity.F目标档位ID);
        if (adjustResult.Code != 200)
            _logger.LogWarning("[Salary] 调档失败 ReviewId={Id} Reason={Reason}", id, adjustResult.Message);

        // 获取新档位信息
        var newGrade = await _context.Set<SalaryGrade>()
            .FirstOrDefaultAsync(g => g.FID == entity.F目标档位ID);

        // 写入晋升历史
        var history = new PromotionHistory
        {
            FOrgId = orgId,
            F员工ID = entity.F员工ID,
            F评审ID = entity.FID,
            F原档位ID = entity.F当前档位ID,
            F新档位ID = entity.F目标档位ID,
            F原基本工资 = oldBaseSalary,
            F新基本工资 = newGrade?.F基本工资 ?? 0,
            F生效日期 = entity.F生效日期 ?? DateTime.Today,
            F创建时间 = DateTime.Now
        };
        _context.Set<PromotionHistory>().Add(history);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[Salary] 晋升通过 ReviewId={Id} EmployeeId={EmpId} NewGradeId={GradeId}",
            id, entity.F员工ID, entity.F目标档位ID);
        return ApiResult.Ok("晋升已通过");
    }

    public async Task<ApiResult> RejectAsync(long orgId, long id, long reviewerId, ReviewPromotionRequest request)
    {
        var entity = await _context.Set<PromotionReview>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult.Fail("评审记录不存在");
        if (entity.F状态 != 0)
            return ApiResult.Fail("只有待评审状态才能驳回");

        entity.F状态 = 2;
        entity.F评审人ID = reviewerId;
        entity.F评审时间 = DateTime.Now;
        entity.F评审意见 = request.Comment;

        await _context.SaveChangesAsync();

        _logger.LogInformation("[Salary] 晋升驳回 ReviewId={Id} EmployeeId={EmpId}", id, entity.F员工ID);
        return ApiResult.Ok("已驳回");
    }

    private static PromotionReviewDto MapToDto(PromotionReview r, Dictionary<long, string> gradeMap, Dictionary<long, string> ruleMap) => new()
    {
        Id = r.FID,
        OrgId = r.FOrgId,
        EmployeeId = r.F员工ID,
        RuleId = r.F规则ID,
        RuleName = ruleMap.GetValueOrDefault(r.F规则ID),
        CurrentGradeId = r.F当前档位ID,
        CurrentGradeName = gradeMap.GetValueOrDefault(r.F当前档位ID),
        TargetGradeId = r.F目标档位ID,
        TargetGradeName = gradeMap.GetValueOrDefault(r.F目标档位ID),
        TriggerTime = r.F触发时间,
        AScoreSnapshot = r.FA分快照,
        Status = r.F状态,
        ReviewerId = r.F评审人ID,
        ReviewTime = r.F评审时间,
        ReviewComment = r.F评审意见,
        EffectiveDate = r.F生效日期,
        CreateTime = r.F创建时间
    };
}
