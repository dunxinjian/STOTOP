using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.PPV.Dtos;
using STOTOP.Module.PPV.Entities;
using STOTOP.Module.PPV.Events;

namespace STOTOP.Module.PPV.Services;

public class PpvRecordService : IPpvRecordService
{
    private readonly STOTOPDbContext _context;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<PpvRecordService> _logger;

    public PpvRecordService(STOTOPDbContext context, IEventDispatcher eventDispatcher, ILogger<PpvRecordService> logger)
    {
        _context = context;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public async Task<ApiResult<List<PpvRecordDto>>> GetListAsync(long orgId, PpvRecordPagedRequest request)
    {
        var query = _context.Set<PpvRecord>()
            .Where(r => r.FOrgId == orgId);

        if (!string.IsNullOrWhiteSpace(request.Period))
            query = query.Where(r => r.F期间 == request.Period);
        if (request.EmployeeId.HasValue)
            query = query.Where(r => r.F员工ID == request.EmployeeId.Value);
        if (request.Status.HasValue)
            query = query.Where(r => r.F审核状态 == request.Status.Value);

        var list = await query
            .OrderByDescending(r => r.FID)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = list.Select(MapToDto).ToList();
        return ApiResult<List<PpvRecordDto>>.Success(dtos);
    }

    public async Task<ApiResult<PpvRecordDto>> CreateAsync(long orgId, long currentUserId, CreatePpvRecordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Period))
            return ApiResult<PpvRecordDto>.Fail("期间不能为空");

        var template = await _context.Set<PpvTemplate>()
            .FirstOrDefaultAsync(t => t.FID == request.TemplateId && t.FOrgId == orgId);
        if (template == null)
            return ApiResult<PpvRecordDto>.Fail("产值模板不存在");
        if (!template.F启用状态)
            return ApiResult<PpvRecordDto>.Fail("产值模板未启用");

        // 产值金额 = 数量 × 模板单价
        var amount = request.Quantity * template.F单价;

        var entity = new PpvRecord
        {
            FOrgId = orgId,
            F员工ID = request.EmployeeId,
            F期间 = request.Period.Trim(),
            F模板ID = request.TemplateId,
            F产值项编码 = template.F产值项编码,
            F数量 = request.Quantity,
            F产值金额 = amount,
            F质量等级 = request.QualityLevel,
            F是否跨岗 = request.IsCrossPosition,
            F审核状态 = 0,
            F创建时间 = DateTime.Now
        };

        _context.Set<PpvRecord>().Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[PPV] 创建记录 Id={Id} EmployeeId={EmpId} Period={Period} Amount={Amount}",
            entity.FID, entity.F员工ID, entity.F期间, amount);

        return ApiResult<PpvRecordDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult<PpvRecordDto>> UpdateAsync(long orgId, long id, UpdatePpvRecordRequest request)
    {
        var entity = await _context.Set<PpvRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult<PpvRecordDto>.Fail("记录不存在");

        // 审核通过后不可编辑
        if (entity.F审核状态 != 0)
            return ApiResult<PpvRecordDto>.Fail("已审核的记录不可编辑");

        // 重新计算产值金额
        var template = await _context.Set<PpvTemplate>()
            .FirstOrDefaultAsync(t => t.FID == entity.F模板ID);
        var unitPrice = template?.F单价 ?? 0;

        entity.F数量 = request.Quantity;
        entity.F产值金额 = request.Quantity * unitPrice;
        entity.F质量等级 = request.QualityLevel;
        entity.F是否跨岗 = request.IsCrossPosition;

        await _context.SaveChangesAsync();
        return ApiResult<PpvRecordDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult> ReviewAsync(long orgId, long id, long reviewerId, ReviewPpvRecordRequest request)
    {
        var entity = await _context.Set<PpvRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id && r.FOrgId == orgId);
        if (entity == null)
            return ApiResult.Fail("记录不存在");
        if (entity.F审核状态 != 0)
            return ApiResult.Fail("该记录已审核，不可重复审核");

        entity.F审核人ID = reviewerId;
        entity.F审核时间 = DateTime.Now;
        entity.F审核备注 = request.Remark;

        if (request.Approve)
        {
            entity.F审核状态 = 1;
            // 重新计算产值金额（以确保数据正确）
            var template = await _context.Set<PpvTemplate>()
                .FirstOrDefaultAsync(t => t.FID == entity.F模板ID);
            if (template != null)
                entity.F产值金额 = entity.F数量 * template.F单价;

            // 发布 PpvWorkRecordedEvent
            try
            {
                await _eventDispatcher.PublishAsync(new PpvWorkRecordedEvent
                {
                    OrgId = orgId,
                    EmployeeId = entity.F员工ID,
                    Period = entity.F期间,
                    RecordId = entity.FID,
                    ProductValue = entity.F产值金额,
                    QualityGrade = entity.F质量等级,
                    IsCrossPosition = entity.F是否跨岗,
                    ModuleCode = "ppv"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PPV] 发布 PpvWorkRecordedEvent 失败，不影响审核主流程: RecordId={Id}", entity.FID);
            }
        }
        else
        {
            entity.F审核状态 = 2;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("[PPV] 审核记录 Id={Id} Approve={Approve} Reviewer={ReviewerId}",
            id, request.Approve, reviewerId);

        return ApiResult.Ok(request.Approve ? "审核通过" : "已驳回");
    }

    public async Task<ApiResult<List<PpvRecordDto>>> GetMyRecordsAsync(long orgId, long employeeId, string? period)
    {
        var query = _context.Set<PpvRecord>()
            .Where(r => r.FOrgId == orgId && r.F员工ID == employeeId);

        if (!string.IsNullOrWhiteSpace(period))
            query = query.Where(r => r.F期间 == period);

        var list = await query
            .OrderByDescending(r => r.F创建时间)
            .ToListAsync();

        var dtos = list.Select(MapToDto).ToList();
        return ApiResult<List<PpvRecordDto>>.Success(dtos);
    }

    private static PpvRecordDto MapToDto(PpvRecord r) => new()
    {
        Id = r.FID,
        OrgId = r.FOrgId,
        EmployeeId = r.F员工ID,
        Period = r.F期间,
        TemplateId = r.F模板ID,
        ItemCode = r.F产值项编码,
        Quantity = r.F数量,
        Amount = r.F产值金额,
        QualityLevel = r.F质量等级,
        IsCrossPosition = r.F是否跨岗,
        ReviewStatus = r.F审核状态,
        ReviewerId = r.F审核人ID,
        ReviewTime = r.F审核时间,
        ReviewRemark = r.F审核备注,
        CreateTime = r.F创建时间
    };
}
