using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Salary.Dtos;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Services;

public class SalaryArchiveService : ISalaryArchiveService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<SalaryArchiveService> _logger;

    public SalaryArchiveService(STOTOPDbContext context, ILogger<SalaryArchiveService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResult<List<SalaryArchiveDto>>> GetListAsync(long orgId)
    {
        var list = await _context.Set<SalaryArchive>()
            .Where(a => a.FOrgId == orgId)
            .OrderByDescending(a => a.FID)
            .ToListAsync();

        var gradeIds = list.Select(a => a.F档位ID).Distinct().ToList();
        var gradeMap = await _context.Set<SalaryGrade>()
            .Where(g => gradeIds.Contains(g.FID))
            .ToDictionaryAsync(g => g.FID, g => g.F档位名称);

        var dtos = list.Select(a => MapToDto(a, gradeMap.GetValueOrDefault(a.F档位ID))).ToList();
        return ApiResult<List<SalaryArchiveDto>>.Success(dtos);
    }

    public async Task<ApiResult<SalaryArchiveDto>> GetByEmployeeAsync(long orgId, long employeeId)
    {
        var entity = await _context.Set<SalaryArchive>()
            .FirstOrDefaultAsync(a => a.FOrgId == orgId && a.F员工ID == employeeId);
        if (entity == null)
            return ApiResult<SalaryArchiveDto>.Fail("该员工无薪酬档案");

        var gradeName = await _context.Set<SalaryGrade>()
            .Where(g => g.FID == entity.F档位ID)
            .Select(g => g.F档位名称)
            .FirstOrDefaultAsync();

        return ApiResult<SalaryArchiveDto>.Success(MapToDto(entity, gradeName));
    }

    public async Task<ApiResult<SalaryArchiveDto>> CreateAsync(long orgId, CreateSalaryArchiveRequest request)
    {
        if (request.EmployeeId <= 0)
            return ApiResult<SalaryArchiveDto>.Fail("员工ID无效");

        var exists = await _context.Set<SalaryArchive>()
            .AnyAsync(a => a.FOrgId == orgId && a.F员工ID == request.EmployeeId);
        if (exists)
            return ApiResult<SalaryArchiveDto>.Fail("该员工已有薪酬档案");

        var entity = new SalaryArchive
        {
            FOrgId = orgId,
            F员工ID = request.EmployeeId,
            F档位ID = request.GradeId,
            F入档日期 = request.EnrollDate,
            F基本工资 = request.BaseSalary,
            F岗位津贴 = request.PositionAllowance,
            F社保基数 = request.SocialInsuranceBase,
            F公积金基数 = request.HousingFundBase,
            F个税起征额 = request.TaxThreshold,
            F备注 = request.Remark,
            F创建时间 = DateTime.Now,
            F更新时间 = DateTime.Now
        };

        _context.Set<SalaryArchive>().Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[Salary] 创建档案 Id={Id} EmployeeId={EmpId} OrgId={OrgId}", entity.FID, entity.F员工ID, orgId);
        return ApiResult<SalaryArchiveDto>.Success(MapToDto(entity, null));
    }

    public async Task<ApiResult<SalaryArchiveDto>> UpdateAsync(long orgId, long id, UpdateSalaryArchiveRequest request)
    {
        var entity = await _context.Set<SalaryArchive>()
            .AsTracking()
            .FirstOrDefaultAsync(a => a.FID == id && a.FOrgId == orgId);
        if (entity == null)
            return ApiResult<SalaryArchiveDto>.Fail("档案不存在");

        entity.F档位ID = request.GradeId;
        entity.F基本工资 = request.BaseSalary;
        entity.F岗位津贴 = request.PositionAllowance;
        entity.F社保基数 = request.SocialInsuranceBase;
        entity.F公积金基数 = request.HousingFundBase;
        entity.F个税起征额 = request.TaxThreshold;
        entity.F备注 = request.Remark;
        entity.F更新时间 = DateTime.Now;

        await _context.SaveChangesAsync();
        return ApiResult<SalaryArchiveDto>.Success(MapToDto(entity, null));
    }

    public async Task<ApiResult> AdjustGradeAsync(long orgId, long employeeId, long newGradeId)
    {
        var archive = await _context.Set<SalaryArchive>()
            .AsTracking()
            .FirstOrDefaultAsync(a => a.FOrgId == orgId && a.F员工ID == employeeId);
        if (archive == null)
            return ApiResult.Fail("员工薪酬档案不存在");

        var newGrade = await _context.Set<SalaryGrade>()
            .FirstOrDefaultAsync(g => g.FID == newGradeId && g.FOrgId == orgId);
        if (newGrade == null)
            return ApiResult.Fail("目标档位不存在");

        archive.F档位ID = newGradeId;
        archive.F基本工资 = newGrade.F基本工资;
        archive.F岗位津贴 = newGrade.F岗位津贴;
        archive.F更新时间 = DateTime.Now;

        await _context.SaveChangesAsync();

        _logger.LogInformation("[Salary] 调档 EmployeeId={EmpId} NewGradeId={GradeId} OrgId={OrgId}", employeeId, newGradeId, orgId);
        return ApiResult.Ok("调档成功");
    }

    private static SalaryArchiveDto MapToDto(SalaryArchive a, string? gradeName) => new()
    {
        Id = a.FID,
        OrgId = a.FOrgId,
        EmployeeId = a.F员工ID,
        GradeId = a.F档位ID,
        GradeName = gradeName,
        EnrollDate = a.F入档日期,
        BaseSalary = a.F基本工资,
        PositionAllowance = a.F岗位津贴,
        SocialInsuranceBase = a.F社保基数,
        HousingFundBase = a.F公积金基数,
        TaxThreshold = a.F个税起征额,
        Remark = a.F备注,
        CreateTime = a.F创建时间,
        UpdateTime = a.F更新时间
    };
}
