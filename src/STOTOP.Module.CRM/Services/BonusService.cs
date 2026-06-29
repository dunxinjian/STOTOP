using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services.Interfaces;

namespace STOTOP.Module.CRM.Services;

public class BonusService : IBonusService
{
    private readonly IRepository<CrmBonusPlan> _planRepo;
    private readonly IRepository<CrmBonusDetail> _detailRepo;

    public BonusService(
        IRepository<CrmBonusPlan> planRepo,
        IRepository<CrmBonusDetail> detailRepo)
    {
        _planRepo = planRepo;
        _detailRepo = detailRepo;
    }

    #region Bonus Plans

    public async Task<PagedResult<BonusPlanDto>> GetBonusPlansAsync(BonusPlanQueryRequest request)
    {
        var query = _planRepo.Query();

        if (request.OrgId.HasValue)
            query = query.Where(p => p.FOrgId == request.OrgId.Value);
        if (!string.IsNullOrWhiteSpace(request.Period))
            query = query.Where(p => p.FPeriod == request.Period);
        if (request.Status.HasValue)
            query = query.Where(p => p.FStatus == request.Status.Value);

        var total = await query.CountAsync();
        var items = await query
            .Include(p => p.Details)
            .OrderByDescending(p => p.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<BonusPlanDto>
        {
            Items = items.Select(MapPlanToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<BonusPlanDto?> GetBonusPlanByIdAsync(long id)
    {
        var entity = await _planRepo.Query()
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.FID == id);
        return entity == null ? null : MapPlanToDto(entity);
    }

    public async Task<BonusPlanDto> CreateBonusPlanAsync(CreateBonusPlanRequest request)
    {
        var entity = new CrmBonusPlan
        {
            FOrgId = request.OrgId ?? 0,
            FPeriod = request.Period,
            FTotalAmount = request.TotalAmount,
            FCalcRules = request.CalcRules,
            FStatus = 0, // 草稿
            FCreatedTime = DateTime.Now
        };

        await _planRepo.AddAsync(entity);

        if (request.Details != null && request.Details.Count > 0)
        {
            foreach (var detailReq in request.Details)
            {
                var detail = new CrmBonusDetail
                {
                    FPlanId = entity.FID,
                    FEmployeeId = detailReq.EmployeeId,
                    FAmount = detailReq.Amount,
                    FBonusType = detailReq.BonusType,
                    FCreatedTime = DateTime.Now
                };
                await _detailRepo.AddAsync(detail);
            }
        }

        return (await GetBonusPlanByIdAsync(entity.FID))!;
    }

    public async Task<BonusPlanDto?> UpdateBonusPlanAsync(long id, UpdateBonusPlanRequest request)
    {
        var entity = await _planRepo.Query().AsTracking()
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.FID == id);
        if (entity == null) return null;

        if (entity.FStatus != 0)
            throw new InvalidOperationException("只有草稿状态的方案可以编辑");

        entity.FOrgId = request.OrgId ?? 0;
        entity.FPeriod = request.Period;
        entity.FTotalAmount = request.TotalAmount;
        entity.FCalcRules = request.CalcRules;
        entity.FUpdatedTime = DateTime.Now;

        await _planRepo.UpdateAsync(entity);

        // 重建明细
        if (request.Details != null)
        {
            foreach (var existing in entity.Details)
            {
                await _detailRepo.DeleteAsync(existing.FID);
            }

            foreach (var detailReq in request.Details)
            {
                var detail = new CrmBonusDetail
                {
                    FPlanId = id,
                    FEmployeeId = detailReq.EmployeeId,
                    FAmount = detailReq.Amount,
                    FBonusType = detailReq.BonusType,
                    FCreatedTime = DateTime.Now
                };
                await _detailRepo.AddAsync(detail);
            }
        }

        return await GetBonusPlanByIdAsync(id);
    }

    public async Task<bool> DeleteBonusPlanAsync(long id)
    {
        var entity = await _planRepo.Query()
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.FID == id);
        if (entity == null) return false;

        if (entity.FStatus != 0)
            throw new InvalidOperationException("只有草稿状态的方案可以删除");

        foreach (var detail in entity.Details)
        {
            await _detailRepo.DeleteAsync(detail.FID);
        }
        await _planRepo.DeleteAsync(id);
        return true;
    }

    public async Task<bool> UpdatePlanStatusAsync(long id, int status)
    {
        var entity = await _planRepo.Query().AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id);
        if (entity == null) return false;

        entity.FStatus = status;
        entity.FUpdatedTime = DateTime.Now;
        await _planRepo.UpdateAsync(entity);
        return true;
    }

    #endregion

    #region Bonus Details

    public async Task<PagedResult<BonusDetailDto>> GetBonusDetailsAsync(BonusDetailQueryRequest request)
    {
        var query = _detailRepo.Query()
            .Include(d => d.Plan)
            .AsQueryable();

        if (request.PlanId.HasValue)
            query = query.Where(d => d.FPlanId == request.PlanId.Value);
        if (request.EmployeeId.HasValue)
            query = query.Where(d => d.FEmployeeId == request.EmployeeId.Value);
        if (request.OrgId.HasValue)
            query = query.Where(d => d.Plan.FOrgId == request.OrgId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<BonusDetailDto>
        {
            Items = items.Select(MapDetailToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<BonusDetailDto> AddBonusDetailAsync(long planId, CreateBonusDetailRequest request)
    {
        var plan = await _planRepo.GetByIdAsync(planId)
            ?? throw new InvalidOperationException("奖金方案不存在");

        if (plan.FStatus != 0)
            throw new InvalidOperationException("只有草稿状态的方案可以添加明细");

        var detail = new CrmBonusDetail
        {
            FPlanId = planId,
            FEmployeeId = request.EmployeeId,
            FAmount = request.Amount,
            FBonusType = request.BonusType,
            FCreatedTime = DateTime.Now
        };

        await _detailRepo.AddAsync(detail);
        return MapDetailToDto(detail);
    }

    public async Task<bool> DeleteBonusDetailAsync(long detailId)
    {
        var detail = await _detailRepo.Query()
            .Include(d => d.Plan)
            .FirstOrDefaultAsync(d => d.FID == detailId);
        if (detail == null) return false;

        if (detail.Plan.FStatus != 0)
            throw new InvalidOperationException("只有草稿状态的方案可以删除明细");

        await _detailRepo.DeleteAsync(detailId);
        return true;
    }

    #endregion

    #region Mapping

    private static BonusPlanDto MapPlanToDto(CrmBonusPlan e) => new()
    {
        Id = e.FID,
        OrgId = e.FOrgId,
        Period = e.FPeriod,
        TotalAmount = e.FTotalAmount,
        CalcRules = e.FCalcRules,
        Status = e.FStatus,
        OaProcessInstanceId = e.FOaProcessInstanceId,
        CreatorName = e.FCreatorName,
        CreatedTime = e.FCreatedTime,
        UpdaterName = e.FUpdaterName,
        UpdatedTime = e.FUpdatedTime,
        Details = e.Details.Select(MapDetailToDto).ToList()
    };

    private static BonusDetailDto MapDetailToDto(CrmBonusDetail e) => new()
    {
        Id = e.FID,
        PlanId = e.FPlanId,
        EmployeeId = e.FEmployeeId,
        Amount = e.FAmount,
        BonusType = e.FBonusType,
        CreatorName = e.FCreatorName,
        CreatedTime = e.FCreatedTime
    };

    #endregion
}
