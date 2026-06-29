using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Quality.Entities;
using STOTOP.Module.Quality.Services.Unification;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Quality.Controllers;

/// <summary>
/// 统一质控——主数据认领 + 归一触发。
/// 提供"待认领员工"查询（前端把脏名归一到主数据/建别名）、手动归一(run)、重跑回填(rematch)。
/// </summary>
[Authorize]
[ApiController]
[Route("api/quality/unify")]
public class UnificationController : ControllerBase
{
    private readonly STOTOPDbContext _db;
    private readonly IQualityUnificationService _unifyService;

    public UnificationController(STOTOPDbContext db, IQualityUnificationService unifyService)
    {
        _db = db;
        _unifyService = unifyService;
    }

    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>
    /// 待认领员工：质量事件中员工匹配状态 ∈ {0 未匹配, 3 启发式候选} 的，
    /// 按 (员工姓名原文, 网点编码) 聚合计数，供前端逐条认领（建别名/绑工号）。
    /// </summary>
    [HttpGet("pending-employees")]
    [RequirePermission(QualityPermissions.CarrierMasterDataClaim)]
    public async Task<ApiResult<List<PendingEmployeeDto>>> GetPendingEmployees()
    {
        var orgId = GetOrgId();

        var items = await _db.Set<QlShentongQualityEvent>()
            .Where(e => e.FOrgId == orgId
                        && (e.F员工匹配状态 == 0 || e.F员工匹配状态 == 3)
                        && e.F员工姓名原文 != null && e.F员工姓名原文 != "")
            .GroupBy(e => new { e.F员工姓名原文, e.F网点编码 })
            .Select(g => new PendingEmployeeDto
            {
                NameRaw = g.Key.F员工姓名原文!,
                NetworkCode = g.Key.F网点编码,
                Count = g.Count(),
                SuggestStatus = g.Max(e => e.F员工匹配状态)
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return ApiResult<List<PendingEmployeeDto>>.Success(items);
    }

    /// <summary>
    /// 手动归一：对当前组织整批归一全申通源（事件 + 员工日指标 + 网点日指标），按唯一键 upsert（幂等）。
    /// </summary>
    [HttpPost("run")]
    [RequirePermission(QualityPermissions.CarrierMasterDataClaim)]
    public async Task<ApiResult<UnifyResult>> Run(CancellationToken ct)
    {
        var orgId = GetOrgId();
        var result = await _unifyService.UnifyShentongAsync(orgId, ct);
        return ApiResult<UnifyResult>.Success(result);
    }

    /// <summary>
    /// 重跑回填：仅重解析当前组织「未匹配」的历史质量事件主数据（不重建事件）。
    /// 用户在「待认领」补别名后调用，使历史未匹配事件命中并回填网点编码/员工工号。
    /// </summary>
    [HttpPost("rematch")]
    [RequirePermission(QualityPermissions.CarrierMasterDataClaim)]
    public async Task<ApiResult<RematchResult>> Rematch(CancellationToken ct)
    {
        var orgId = GetOrgId();
        var result = await _unifyService.RematchUnresolvedAsync(orgId, ct);
        return ApiResult<RematchResult>.Success(result);
    }
}

/// <summary>待认领员工聚合项</summary>
public class PendingEmployeeDto
{
    /// <summary>员工姓名原文（脏名）</summary>
    public string NameRaw { get; set; } = string.Empty;
    /// <summary>网点编码（可空）</summary>
    public string? NetworkCode { get; set; }
    /// <summary>该脏名命中的质量事件条数</summary>
    public int Count { get; set; }
    /// <summary>聚合内最高匹配状态（0 纯未匹配 / 3 含启发式候选建议）</summary>
    public int SuggestStatus { get; set; }
}
