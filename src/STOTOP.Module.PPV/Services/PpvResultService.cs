using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.PPV.Dtos;
using STOTOP.Module.PPV.Entities;

namespace STOTOP.Module.PPV.Services;

public class PpvResultService : IPpvResultService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<PpvResultService> _logger;

    public PpvResultService(STOTOPDbContext context, ILogger<PpvResultService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResult<List<PpvMonthlyResultDto>>> GetListAsync(long orgId, string? period, long? employeeId, int page, int pageSize)
    {
        var query = _context.Set<PpvMonthlyResult>()
            .Where(r => r.FOrgId == orgId);

        if (!string.IsNullOrWhiteSpace(period))
            query = query.Where(r => r.F期间 == period);
        if (employeeId.HasValue)
            query = query.Where(r => r.F员工ID == employeeId.Value);

        var list = await query
            .OrderByDescending(r => r.F期间)
            .ThenByDescending(r => r.FID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = list.Select(MapToDto).ToList();
        return ApiResult<List<PpvMonthlyResultDto>>.Success(dtos);
    }

    public async Task<ApiResult<PpvMonthlyResultDto>> GetDetailAsync(long orgId, long id)
    {
        var entity = await _context.Set<PpvMonthlyResult>()
            .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult<PpvMonthlyResultDto>.Fail("月度汇总记录不存在");

        return ApiResult<PpvMonthlyResultDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult<List<PpvMonthlyResultDto>>> GetMyResultsAsync(long orgId, long employeeId, string? period)
    {
        var query = _context.Set<PpvMonthlyResult>()
            .Where(r => r.FOrgId == orgId && r.F员工ID == employeeId);

        if (!string.IsNullOrWhiteSpace(period))
            query = query.Where(r => r.F期间 == period);

        var list = await query
            .OrderByDescending(r => r.F期间)
            .ToListAsync();

        var dtos = list.Select(MapToDto).ToList();
        return ApiResult<List<PpvMonthlyResultDto>>.Success(dtos);
    }

    private static PpvMonthlyResultDto MapToDto(PpvMonthlyResult r) => new()
    {
        Id = r.FID,
        OrgId = r.FOrgId,
        EmployeeId = r.F员工ID,
        Period = r.F期间,
        TotalAmount = r.F总产值,
        OwnPositionAmount = r.F本岗产值,
        CrossPositionAmount = r.F跨岗产值,
        ComprehensiveQualityLevel = r.F综合质量等级,
        IsCrossPositionCleared = r.F是否跨岗清零,
        ClearReason = r.F清零原因,
        BScoreChange = r.FB分变化,
        AScoreChange = r.FA分变化,
        PositionIdSnapshot = r.F岗位ID快照,
        DepartmentIdSnapshot = r.F部门ID快照,
        Status = r.F状态,
        CreateTime = r.F创建时间
    };
}
