using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Salary.Dtos;
using STOTOP.Module.Salary.Entities;
using STOTOP.Module.Salary.Events;

namespace STOTOP.Module.Salary.Services;

public class SalaryPayrollService : ISalaryPayrollService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<SalaryPayrollService> _logger;
    private readonly IEventDispatcher _eventDispatcher;

    public SalaryPayrollService(STOTOPDbContext context, ILogger<SalaryPayrollService> logger, IEventDispatcher eventDispatcher)
    {
        _context = context;
        _logger = logger;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<ApiResult<List<SalaryPayrollDto>>> GetListAsync(long orgId, string? period = null, int? status = null)
    {
        var query = _context.Set<SalaryPayroll>()
            .Where(p => p.FOrgId == orgId);

        if (!string.IsNullOrWhiteSpace(period))
            query = query.Where(p => p.F期间 == period);
        if (status.HasValue)
            query = query.Where(p => p.F状态 == status.Value);

        var list = await query
            .OrderByDescending(p => p.F期间)
            .ThenByDescending(p => p.FID)
            .ToListAsync();

        var dtos = list.Select(p => MapToDto(p, null)).ToList();
        return ApiResult<List<SalaryPayrollDto>>.Success(dtos);
    }

    public async Task<ApiResult<SalaryPayrollDto>> GetDetailAsync(long orgId, long id)
    {
        var entity = await _context.Set<SalaryPayroll>()
            .FirstOrDefaultAsync(p => p.FID == id && p.FOrgId == orgId);
        if (entity == null)
            return ApiResult<SalaryPayrollDto>.Fail("工资单不存在");

        var details = await _context.Set<SalaryPayrollDetail>()
            .Where(d => d.F工资单ID == id)
            .ToListAsync();

        var detailDtos = details.Select(d => new SalaryPayrollDetailDto
        {
            Id = d.FID,
            PayrollId = d.F工资单ID,
            ItemType = d.F项目类型,
            ItemName = d.F项目名称,
            Amount = d.F金额,
            SourceId = d.F来源ID,
            SourceType = d.F来源类型,
            Remark = d.F备注
        }).ToList();

        return ApiResult<SalaryPayrollDto>.Success(MapToDto(entity, detailDtos));
    }

    public async Task<ApiResult<List<SalaryPayrollDto>>> GetMyPayrollAsync(long orgId, long userId, int count = 12)
    {
        var list = await _context.Set<SalaryPayroll>()
            .Where(p => p.FOrgId == orgId && p.F员工ID == userId)
            .OrderByDescending(p => p.F期间)
            .Take(count)
            .ToListAsync();

        var dtos = list.Select(p => MapToDto(p, null)).ToList();
        return ApiResult<List<SalaryPayrollDto>>.Success(dtos);
    }

    public async Task<ApiResult> AuditAsync(long orgId, long id, long auditorId)
    {
        var entity = await _context.Set<SalaryPayroll>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id && p.FOrgId == orgId);
        if (entity == null)
            return ApiResult.Fail("工资单不存在");
        if (entity.F状态 != 1)
            return ApiResult.Fail("只有待审状态的工资单才能审核");

        entity.F状态 = 2;
        entity.F审核人ID = auditorId;
        entity.F审核时间 = DateTime.Now;

        await _context.SaveChangesAsync();

        _logger.LogInformation("[Salary] 工资单审核 Id={Id} AuditorId={AuditorId}", id, auditorId);
        return ApiResult.Ok("审核通过");
    }

    public async Task<ApiResult> ReleaseAsync(long orgId, long id)
    {
        var entity = await _context.Set<SalaryPayroll>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id && p.FOrgId == orgId);
        if (entity == null)
            return ApiResult.Fail("工资单不存在");
        if (entity.F状态 != 2)
            return ApiResult.Fail("只有已审状态的工资单才能发放");

        entity.F状态 = 3;
        entity.F发放时间 = DateTime.Now;

        await _context.SaveChangesAsync();

        // 发布 SalaryReleasedEvent（不影响主流程）
        try
        {
            await _eventDispatcher.PublishAsync(new SalaryReleasedEvent
            {
                OrgId = orgId,
                ModuleCode = "Salary",
                EmployeeId = entity.F员工ID,
                Period = entity.F期间,
                PayrollId = entity.FID,
                NetAmount = entity.F实发合计
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Salary] SalaryReleasedEvent 发布失败 Id={Id}", id);
        }

        _logger.LogInformation("[Salary] 工资单发放 Id={Id} Period={Period}", id, entity.F期间);
        return ApiResult.Ok("已发放");
    }

    public async Task<ApiResult> RecalcAsync(long orgId, RecalcPayrollRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Period))
            return ApiResult.Fail("期间不能为空");

        // TODO: 实际重算逻辑将由 SalarySettlementJob 实现
        _logger.LogInformation("[Salary] 触发重算 Period={Period} OrgId={OrgId}", request.Period, orgId);
        return ApiResult.Ok("已提交重算任务");
    }

    private static SalaryPayrollDto MapToDto(SalaryPayroll p, List<SalaryPayrollDetailDto>? details) => new()
    {
        Id = p.FID,
        OrgId = p.FOrgId,
        EmployeeId = p.F员工ID,
        Period = p.F期间,
        BaseSalary = p.F基本工资,
        KsfFloat = p.FKSF浮动,
        PpvBonus = p.FPPV奖金,
        BPointExchange = p.FB分兑换,
        AttendanceDeduction = p.F考勤扣减,
        SocialInsurancePersonal = p.F社保个人,
        HousingFundPersonal = p.F公积金个人,
        IncomeTax = p.F个税,
        GrossTotal = p.F应发合计,
        NetTotal = p.F实发合计,
        Status = p.F状态,
        AuditorId = p.F审核人ID,
        AuditTime = p.F审核时间,
        ReleaseTime = p.F发放时间,
        CreateTime = p.F创建时间,
        Details = details ?? new()
    };
}
