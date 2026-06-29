using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Points.Services;

public class ManagerQuotaService : IManagerQuotaService
{
    private readonly STOTOPDbContext _db;

    public ManagerQuotaService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<ManagerQuotaListDto>>> GetPagedListAsync(long orgId, ManagerQuotaPagedRequest request)
    {
        var query = _db.Set<PmManagerQuota>()
            .Where(q => q.FOrgId == orgId)
            .AsQueryable();

        if (request.ManagerId.HasValue)
            query = query.Where(q => q.FManagerId == request.ManagerId.Value);
        if (!string.IsNullOrWhiteSpace(request.YearMonth))
            query = query.Where(q => q.FYearMonth == request.YearMonth);
        if (request.Status.HasValue)
            query = query.Where(q => q.FStatus == request.Status.Value);

        var total = await query.CountAsync();

        var quotas = await query
            .OrderByDescending(q => q.FYearMonth)
            .ThenBy(q => q.FManagerId)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // 批量获取管理员名
        var managerIds = quotas.Select(q => q.FManagerId).Distinct().ToList();
        var managers = await _db.Set<SysUser>()
            .Where(u => managerIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var managerDict = managers.ToDictionary(u => u.FID, u => u.FName);

        var items = quotas.Select(q => new ManagerQuotaListDto
        {
            Id = q.FID,
            OrgId = q.FOrgId,
            ManagerId = q.FManagerId,
            ManagerName = managerDict.GetValueOrDefault(q.FManagerId),
            YearMonth = q.FYearMonth,
            AwardQuota = q.FAwardQuota,
            DeductQuota = q.FDeductQuota,
            UsedAward = q.FUsedAward,
            UsedDeduct = q.FUsedDeduct,
            Status = q.FStatus,
            CreateTime = q.FCreateTime
        }).ToList();

        return ApiResult<PagedResult<ManagerQuotaListDto>>.Success(new PagedResult<ManagerQuotaListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<ManagerQuotaListDto>> SaveAsync(long orgId, SaveManagerQuotaRequest request)
    {
        // 查找是否已有配额记录
        var entity = await _db.Set<PmManagerQuota>()
            .AsTracking()
            .FirstOrDefaultAsync(q => q.FOrgId == orgId
                && q.FManagerId == request.ManagerId
                && q.FYearMonth == request.YearMonth);

        if (entity == null)
        {
            // 创建新配额
            entity = new PmManagerQuota
            {
                FOrgId = orgId,
                FManagerId = request.ManagerId,
                FYearMonth = request.YearMonth,
                FAwardQuota = request.AwardQuota,
                FDeductQuota = request.DeductQuota,
                FUsedAward = 0,
                FUsedDeduct = 0,
                FStatus = 1, // 1=生效
                FCreateTime = DateTime.Now
            };
            _db.Set<PmManagerQuota>().Add(entity);
        }
        else
        {
            // 更新配额（不重置已使用量）
            entity.FAwardQuota = request.AwardQuota;
            entity.FDeductQuota = request.DeductQuota;
        }

        await _db.SaveChangesAsync();

        // 获取管理员名
        var manager = await _db.Set<SysUser>()
            .Where(u => u.FID == entity.FManagerId)
            .Select(u => new { u.FName })
            .FirstOrDefaultAsync();

        return ApiResult<ManagerQuotaListDto>.Success(new ManagerQuotaListDto
        {
            Id = entity.FID,
            OrgId = entity.FOrgId,
            ManagerId = entity.FManagerId,
            ManagerName = manager?.FName,
            YearMonth = entity.FYearMonth,
            AwardQuota = entity.FAwardQuota,
            DeductQuota = entity.FDeductQuota,
            UsedAward = entity.FUsedAward,
            UsedDeduct = entity.FUsedDeduct,
            Status = entity.FStatus,
            CreateTime = entity.FCreateTime
        });
    }

    public async Task<ApiResult<MyQuotaDto>> GetMyQuotaAsync(long orgId, long managerId)
    {
        var yearMonth = DateTime.Now.ToString("yyyy-MM");

        var entity = await _db.Set<PmManagerQuota>()
            .FirstOrDefaultAsync(q => q.FOrgId == orgId
                && q.FManagerId == managerId
                && q.FYearMonth == yearMonth);

        if (entity == null)
        {
            return ApiResult<MyQuotaDto>.Success(new MyQuotaDto
            {
                YearMonth = yearMonth,
                AwardQuota = 0,
                DeductQuota = 0,
                UsedAward = 0,
                UsedDeduct = 0,
                RemainingAward = 0,
                RemainingDeduct = 0,
                Status = 0
            });
        }

        return ApiResult<MyQuotaDto>.Success(new MyQuotaDto
        {
            Id = entity.FID,
            YearMonth = entity.FYearMonth,
            AwardQuota = entity.FAwardQuota,
            DeductQuota = entity.FDeductQuota,
            UsedAward = entity.FUsedAward,
            UsedDeduct = entity.FUsedDeduct,
            RemainingAward = entity.FAwardQuota - entity.FUsedAward,
            RemainingDeduct = entity.FDeductQuota - entity.FUsedDeduct,
            Status = entity.FStatus
        });
    }

    public async Task<ApiResult<bool>> UseQuotaAsync(long orgId, long managerId, int points, bool isAward)
    {
        var yearMonth = DateTime.Now.ToString("yyyy-MM");

        var entity = await _db.Set<PmManagerQuota>()
            .AsTracking()
            .FirstOrDefaultAsync(q => q.FOrgId == orgId
                && q.FManagerId == managerId
                && q.FYearMonth == yearMonth
                && q.FStatus == 1);

        if (entity == null)
            return ApiResult<bool>.Fail("当月无配额或配额未生效");

        if (isAward)
        {
            var remaining = entity.FAwardQuota - entity.FUsedAward;
            if (remaining < points)
                return ApiResult<bool>.Fail($"奖分配额不足，剩余{remaining}分");

            entity.FUsedAward += points;
        }
        else
        {
            var remaining = entity.FDeductQuota - entity.FUsedDeduct;
            if (remaining < points)
                return ApiResult<bool>.Fail($"扣分配额不足，剩余{remaining}分");

            entity.FUsedDeduct += points;
        }

        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }
}
