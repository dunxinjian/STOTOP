using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.KSF.Dtos;
using STOTOP.Module.KSF.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.KSF.Services;

public class KsfResultService : IKsfResultService
{
    private readonly STOTOPDbContext _context;

    public KsfResultService(STOTOPDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResult<List<KsfResultDto>>> GetListAsync(long orgId, string? period = null, long? employeeId = null, int? status = null)
    {
        var query = _context.Set<KsfResult>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(period))
            query = query.Where(r => r.F期间 == period);
        if (employeeId.HasValue)
            query = query.Where(r => r.F员工ID == employeeId.Value);
        if (status.HasValue)
            query = query.Where(r => r.F状态 == status.Value);

        var results = await query
            .OrderByDescending(r => r.F期间)
            .ThenByDescending(r => r.FID)
            .Take(500)
            .ToListAsync();

        var dtos = await EnrichAsync(results, includeDetails: false);
        return ApiResult<List<KsfResultDto>>.Success(dtos);
    }

    public async Task<ApiResult<KsfResultDto>> GetDetailAsync(long orgId, long resultId)
    {
        var result = await _context.Set<KsfResult>()
            .FirstOrDefaultAsync(r => r.FID == resultId);
        if (result == null)
            return ApiResult<KsfResultDto>.Fail("核算结果不存在");

        var dtos = await EnrichAsync(new List<KsfResult> { result }, includeDetails: true);
        return ApiResult<KsfResultDto>.Success(dtos.First());
    }

    public async Task<ApiResult<List<KsfResultDto>>> GetMyResultsAsync(long orgId, long userId, int count = 12)
    {
        if (count <= 0) count = 12;
        if (count > 60) count = 60;

        var results = await _context.Set<KsfResult>()
            .Where(r => r.F员工ID == userId)
            .OrderByDescending(r => r.F期间)
            .Take(count)
            .ToListAsync();

        var dtos = await EnrichAsync(results, includeDetails: false);
        return ApiResult<List<KsfResultDto>>.Success(dtos);
    }

    private async Task<List<KsfResultDto>> EnrichAsync(List<KsfResult> results, bool includeDetails)
    {
        if (results.Count == 0) return new List<KsfResultDto>();

        var employeeIds = results.Select(r => r.F员工ID).Distinct().ToList();
        var planIds = results.Select(r => r.F方案ID).Distinct().ToList();

        var employees = await _context.Set<SysUser>()
            .Where(u => employeeIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var employeeMap = employees.ToDictionary(u => u.FID, u => u.FName);

        var plans = await _context.Set<KsfPlan>()
            .Where(p => planIds.Contains(p.FID))
            .Select(p => new { p.FID, p.F名称 })
            .ToListAsync();
        var planMap = plans.ToDictionary(p => p.FID, p => p.F名称);

        Dictionary<long, List<KsfResultDetailDto>> detailMap = new();
        if (includeDetails)
        {
            var resultIds = results.Select(r => r.FID).ToList();
            var details = await _context.Set<KsfResultDetail>()
                .Where(d => resultIds.Contains(d.F结果ID))
                .ToListAsync();

            var indicatorIds = details.Select(d => d.F指标ID).Distinct().ToList();
            var indicators = await _context.Set<KsfIndicator>()
                .Where(i => indicatorIds.Contains(i.FID))
                .Select(i => new { i.FID, i.F编码, i.F名称, i.F计量单位 })
                .ToListAsync();
            var indicatorMap = indicators.ToDictionary(i => i.FID);

            detailMap = details.GroupBy(d => d.F结果ID).ToDictionary(
                g => g.Key,
                g => g.Select(d => new KsfResultDetailDto
                {
                    Id = d.FID,
                    ResultId = d.F结果ID,
                    IndicatorId = d.F指标ID,
                    IndicatorCode = indicatorMap.GetValueOrDefault(d.F指标ID)?.F编码,
                    IndicatorName = indicatorMap.GetValueOrDefault(d.F指标ID)?.F名称,
                    IndicatorUnit = indicatorMap.GetValueOrDefault(d.F指标ID)?.F计量单位,
                    ActualValue = d.F实际值,
                    Diff = d.F差额,
                    AmountChange = d.F金额变动,
                    IndicatorSnapshotJson = d.F指标快照JSON
                }).ToList());
        }

        return results.Select(r => new KsfResultDto
        {
            Id = r.FID,
            OrgId = r.FOrgId,
            EmployeeId = r.F员工ID,
            EmployeeName = employeeMap.GetValueOrDefault(r.F员工ID),
            Period = r.F期间,
            PlanId = r.F方案ID,
            PlanName = planMap.GetValueOrDefault(r.F方案ID),
            PositionIdSnapshot = r.F岗位ID快照,
            DepartmentIdSnapshot = r.F部门ID快照,
            BusinessUnitIdSnapshot = r.F经营单元ID快照,
            FixedPart = r.F固定部分,
            FloatingPart = r.F浮动部分,
            Raise = r.F加薪,
            Deduction = r.F扣减,
            NetPayout = r.F实发,
            PlanSnapshotJson = r.F方案快照JSON,
            Status = r.F状态,
            CreateTime = r.F创建时间,
            Details = detailMap.GetValueOrDefault(r.FID, new List<KsfResultDetailDto>())
        }).ToList();
    }
}
