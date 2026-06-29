using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Salary.Dtos;
using STOTOP.Module.Salary.Services;

namespace STOTOP.Module.Salary.Controllers;

[Authorize]
[ApiController]
[Route("api/salary")]
public class SalaryController : ControllerBase
{
    private readonly ISalaryGradeService _gradeService;
    private readonly ISalaryArchiveService _archiveService;
    private readonly ISalaryPayrollService _payrollService;
    private readonly IPromotionRuleService _promotionRuleService;
    private readonly IPromotionReviewService _promotionReviewService;

    public SalaryController(
        ISalaryGradeService gradeService,
        ISalaryArchiveService archiveService,
        ISalaryPayrollService payrollService,
        IPromotionRuleService promotionRuleService,
        IPromotionReviewService promotionReviewService)
    {
        _gradeService = gradeService;
        _archiveService = archiveService;
        _payrollService = payrollService;
        _promotionRuleService = promotionRuleService;
        _promotionReviewService = promotionReviewService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    // ===== 薪酬档位 =====

    /// <summary>档位列表</summary>
    [HttpGet("grades")]
    [RequirePermission(SalaryPermissions.GradeView)]
    public Task<ApiResult<List<SalaryGradeDto>>> GetGrades()
        => _gradeService.GetListAsync(GetOrgId());

    /// <summary>创建档位</summary>
    [HttpPost("grades")]
    [RequirePermission(SalaryPermissions.GradeEdit)]
    public Task<ApiResult<SalaryGradeDto>> CreateGrade([FromBody] CreateSalaryGradeRequest request)
        => _gradeService.CreateAsync(GetOrgId(), request);

    /// <summary>更新档位</summary>
    [HttpPut("grades/{id:long}")]
    [RequirePermission(SalaryPermissions.GradeEdit)]
    public Task<ApiResult<SalaryGradeDto>> UpdateGrade(long id, [FromBody] UpdateSalaryGradeRequest request)
        => _gradeService.UpdateAsync(GetOrgId(), id, request);

    /// <summary>切换档位启用状态</summary>
    [HttpPost("grades/{id:long}/enable")]
    [RequirePermission(SalaryPermissions.GradeEdit)]
    public Task<ApiResult> EnableGrade(long id)
        => _gradeService.EnableAsync(GetOrgId(), id);

    // ===== 员工薪酬档案 =====

    /// <summary>档案列表</summary>
    [HttpGet("archives")]
    [RequirePermission(SalaryPermissions.ArchiveView)]
    public Task<ApiResult<List<SalaryArchiveDto>>> GetArchives()
        => _archiveService.GetListAsync(GetOrgId());

    /// <summary>按员工查询档案</summary>
    [HttpGet("archives/employee/{employeeId:long}")]
    [RequirePermission(SalaryPermissions.ArchiveView)]
    public Task<ApiResult<SalaryArchiveDto>> GetArchiveByEmployee(long employeeId)
        => _archiveService.GetByEmployeeAsync(GetOrgId(), employeeId);

    /// <summary>创建档案</summary>
    [HttpPost("archives")]
    [RequirePermission(SalaryPermissions.ArchiveEdit)]
    public Task<ApiResult<SalaryArchiveDto>> CreateArchive([FromBody] CreateSalaryArchiveRequest request)
        => _archiveService.CreateAsync(GetOrgId(), request);

    /// <summary>更新档案</summary>
    [HttpPut("archives/{id:long}")]
    [RequirePermission(SalaryPermissions.ArchiveEdit)]
    public Task<ApiResult<SalaryArchiveDto>> UpdateArchive(long id, [FromBody] UpdateSalaryArchiveRequest request)
        => _archiveService.UpdateAsync(GetOrgId(), id, request);

    // ===== 月度工资单 =====

    /// <summary>工资单列表</summary>
    [HttpGet("payrolls")]
    [RequirePermission(SalaryPermissions.PayrollView)]
    public Task<ApiResult<List<SalaryPayrollDto>>> GetPayrolls(
        [FromQuery] string? period = null,
        [FromQuery] int? status = null)
        => _payrollService.GetListAsync(GetOrgId(), period, status);

    /// <summary>工资单详情</summary>
    [HttpGet("payrolls/{id:long}")]
    [RequirePermission(SalaryPermissions.PayrollView)]
    public Task<ApiResult<SalaryPayrollDto>> GetPayrollDetail(long id)
        => _payrollService.GetDetailAsync(GetOrgId(), id);

    /// <summary>我的工资单</summary>
    [HttpGet("payrolls/my")]
    [Authorize]
    public Task<ApiResult<List<SalaryPayrollDto>>> GetMyPayroll([FromQuery] int count = 12)
        => _payrollService.GetMyPayrollAsync(GetOrgId(), GetUserId(), count);

    /// <summary>审核工资单</summary>
    [HttpPost("payrolls/{id:long}/audit")]
    [RequirePermission(SalaryPermissions.PayrollAudit)]
    public Task<ApiResult> AuditPayroll(long id)
        => _payrollService.AuditAsync(GetOrgId(), id, GetUserId());

    /// <summary>发放工资单</summary>
    [HttpPost("payrolls/{id:long}/release")]
    [RequirePermission(SalaryPermissions.PayrollRelease)]
    public Task<ApiResult> ReleasePayroll(long id)
        => _payrollService.ReleaseAsync(GetOrgId(), id);

    /// <summary>重算工资单</summary>
    [HttpPost("payrolls/recalc")]
    [RequirePermission(SalaryPermissions.PayrollAudit)]
    public Task<ApiResult> RecalcPayroll([FromBody] RecalcPayrollRequest request)
        => _payrollService.RecalcAsync(GetOrgId(), request);

    // ===== 晋升规则 =====

    /// <summary>晋升规则列表</summary>
    [HttpGet("promotion/rules")]
    [RequirePermission(SalaryPermissions.PromotionView)]
    public Task<ApiResult<List<PromotionRuleDto>>> GetPromotionRules()
        => _promotionRuleService.GetListAsync(GetOrgId());

    /// <summary>创建晋升规则</summary>
    [HttpPost("promotion/rules")]
    [RequirePermission(SalaryPermissions.PromotionRuleEdit)]
    public Task<ApiResult<PromotionRuleDto>> CreatePromotionRule([FromBody] CreatePromotionRuleRequest request)
        => _promotionRuleService.CreateAsync(GetOrgId(), request);

    /// <summary>更新晋升规则</summary>
    [HttpPut("promotion/rules/{id:long}")]
    [RequirePermission(SalaryPermissions.PromotionRuleEdit)]
    public Task<ApiResult<PromotionRuleDto>> UpdatePromotionRule(long id, [FromBody] UpdatePromotionRuleRequest request)
        => _promotionRuleService.UpdateAsync(GetOrgId(), id, request);

    /// <summary>切换晋升规则启用状态</summary>
    [HttpPost("promotion/rules/{id:long}/enable")]
    [RequirePermission(SalaryPermissions.PromotionRuleEdit)]
    public Task<ApiResult> EnablePromotionRule(long id)
        => _promotionRuleService.EnableAsync(GetOrgId(), id);

    // ===== 晋升评审 =====

    /// <summary>评审列表</summary>
    [HttpGet("promotion/reviews")]
    [RequirePermission(SalaryPermissions.PromotionView)]
    public Task<ApiResult<List<PromotionReviewDto>>> GetPromotionReviews()
        => _promotionReviewService.GetListAsync(GetOrgId());

    /// <summary>待评审列表</summary>
    [HttpGet("promotion/reviews/pending")]
    [RequirePermission(SalaryPermissions.PromotionReview)]
    public Task<ApiResult<List<PromotionReviewDto>>> GetPendingReviews()
        => _promotionReviewService.GetPendingListAsync(GetOrgId());

    /// <summary>创建评审</summary>
    [HttpPost("promotion/reviews")]
    [RequirePermission(SalaryPermissions.PromotionReview)]
    public Task<ApiResult<PromotionReviewDto>> CreatePromotionReview([FromBody] CreatePromotionReviewRequest request)
        => _promotionReviewService.CreateAsync(GetOrgId(), request);

    /// <summary>通过晋升</summary>
    [HttpPost("promotion/reviews/{id:long}/approve")]
    [RequirePermission(SalaryPermissions.PromotionReview)]
    public Task<ApiResult> ApprovePromotion(long id, [FromBody] ReviewPromotionRequest request)
        => _promotionReviewService.ApproveAsync(GetOrgId(), id, GetUserId(), request);

    /// <summary>驳回晋升</summary>
    [HttpPost("promotion/reviews/{id:long}/reject")]
    [RequirePermission(SalaryPermissions.PromotionReview)]
    public Task<ApiResult> RejectPromotion(long id, [FromBody] ReviewPromotionRequest request)
        => _promotionReviewService.RejectAsync(GetOrgId(), id, GetUserId(), request);
}
