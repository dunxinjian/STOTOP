using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Salary.Dtos;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Services;

public class SalaryGradeService : ISalaryGradeService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<SalaryGradeService> _logger;

    public SalaryGradeService(STOTOPDbContext context, ILogger<SalaryGradeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResult<List<SalaryGradeDto>>> GetListAsync(long orgId)
    {
        var list = await _context.Set<SalaryGrade>()
            .Where(g => g.FOrgId == orgId)
            .OrderBy(g => g.F级别)
            .ToListAsync();

        var dtos = list.Select(MapToDto).ToList();
        return ApiResult<List<SalaryGradeDto>>.Success(dtos);
    }

    public async Task<ApiResult<SalaryGradeDto>> CreateAsync(long orgId, CreateSalaryGradeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GradeCode))
            return ApiResult<SalaryGradeDto>.Fail("档位编码不能为空");
        if (string.IsNullOrWhiteSpace(request.GradeName))
            return ApiResult<SalaryGradeDto>.Fail("档位名称不能为空");

        var exists = await _context.Set<SalaryGrade>()
            .AnyAsync(g => g.FOrgId == orgId && g.F档位编码 == request.GradeCode.Trim());
        if (exists)
            return ApiResult<SalaryGradeDto>.Fail("档位编码已存在");

        var entity = new SalaryGrade
        {
            FOrgId = orgId,
            F档位编码 = request.GradeCode.Trim(),
            F档位名称 = request.GradeName.Trim(),
            F级别 = request.Level,
            F基本工资 = request.BaseSalary,
            F岗位津贴 = request.PositionAllowance,
            F绩效基数 = request.PerformanceBase,
            F生效起期 = request.EffectiveFrom,
            F启用状态 = request.IsEnabled,
            F创建时间 = DateTime.Now,
            F更新时间 = DateTime.Now
        };

        _context.Set<SalaryGrade>().Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[Salary] 创建档位 Id={Id} Code={Code} OrgId={OrgId}", entity.FID, entity.F档位编码, orgId);
        return ApiResult<SalaryGradeDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult<SalaryGradeDto>> UpdateAsync(long orgId, long id, UpdateSalaryGradeRequest request)
    {
        var entity = await _context.Set<SalaryGrade>()
            .AsTracking()
            .FirstOrDefaultAsync(g => g.FID == id && g.FOrgId == orgId);
        if (entity == null)
            return ApiResult<SalaryGradeDto>.Fail("档位不存在");

        entity.F档位名称 = request.GradeName.Trim();
        entity.F级别 = request.Level;
        entity.F基本工资 = request.BaseSalary;
        entity.F岗位津贴 = request.PositionAllowance;
        entity.F绩效基数 = request.PerformanceBase;
        entity.F生效起期 = request.EffectiveFrom;
        entity.F启用状态 = request.IsEnabled;
        entity.F更新时间 = DateTime.Now;

        await _context.SaveChangesAsync();
        return ApiResult<SalaryGradeDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult> EnableAsync(long orgId, long id)
    {
        var entity = await _context.Set<SalaryGrade>()
            .AsTracking()
            .FirstOrDefaultAsync(g => g.FID == id && g.FOrgId == orgId);
        if (entity == null)
            return ApiResult.Fail("档位不存在");

        entity.F启用状态 = !entity.F启用状态;
        entity.F更新时间 = DateTime.Now;
        await _context.SaveChangesAsync();

        return ApiResult.Ok(entity.F启用状态 ? "已启用" : "已禁用");
    }

    private static SalaryGradeDto MapToDto(SalaryGrade g) => new()
    {
        Id = g.FID,
        OrgId = g.FOrgId,
        GradeCode = g.F档位编码,
        GradeName = g.F档位名称,
        Level = g.F级别,
        BaseSalary = g.F基本工资,
        PositionAllowance = g.F岗位津贴,
        PerformanceBase = g.F绩效基数,
        EffectiveFrom = g.F生效起期,
        IsEnabled = g.F启用状态,
        CreateTime = g.F创建时间,
        UpdateTime = g.F更新时间
    };
}
