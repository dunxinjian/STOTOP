using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.KSF.Dtos;
using STOTOP.Module.KSF.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.KSF.Services;

public class KsfMappingService : IKsfMappingService
{
    private readonly STOTOPDbContext _context;

    public KsfMappingService(STOTOPDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResult<List<KsfMappingDto>>> GetListAsync(long orgId, long? employeeId = null)
    {
        var query = _context.Set<KsfEmployeeUnitMapping>().AsQueryable();
        if (employeeId.HasValue)
            query = query.Where(m => m.F员工ID == employeeId.Value);

        var mappings = await query
            .OrderByDescending(m => m.F生效起期)
            .ThenByDescending(m => m.FID)
            .ToListAsync();

        var employeeIds = mappings.Select(m => m.F员工ID).Distinct().ToList();
        var employees = await _context.Set<SysUser>()
            .Where(u => employeeIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var employeeMap = employees.ToDictionary(u => u.FID, u => u.FName);

        var dtos = mappings.Select(m => new KsfMappingDto
        {
            Id = m.FID,
            OrgId = m.FOrgId,
            EmployeeId = m.F员工ID,
            EmployeeName = employeeMap.GetValueOrDefault(m.F员工ID),
            BusinessUnitId = m.F经营单元ID,
            BusinessUnitName = null,
            Ratio = m.F分摊比例,
            EffectiveFrom = m.F生效起期,
            EffectiveTo = m.F生效止期,
            CreateTime = m.F创建时间
        }).ToList();

        return ApiResult<List<KsfMappingDto>>.Success(dtos);
    }

    public async Task<ApiResult> BatchSaveAsync(long orgId, List<KsfMappingBatchRequest> mappings)
    {
        if (mappings == null || mappings.Count == 0)
            return ApiResult.Fail("映射列表不能为空");

        // 校验每行：分摊比例之和（同员工同期间）不超过 1
        foreach (var m in mappings)
        {
            if (m.EmployeeId <= 0 || m.BusinessUnitId <= 0)
                return ApiResult.Fail("员工或经营单元 ID 无效");
            if (m.Ratio <= 0 || m.Ratio > 1m)
                return ApiResult.Fail($"分摊比例必须在 (0, 1] 区间：员工 {m.EmployeeId}");
        }

        var idsToUpdate = mappings.Where(m => m.Id.HasValue && m.Id.Value > 0).Select(m => m.Id!.Value).ToList();
        var existing = idsToUpdate.Count > 0
            ? await _context.Set<KsfEmployeeUnitMapping>()
                .AsTracking()
                .Where(m => idsToUpdate.Contains(m.FID))
                .ToListAsync()
            : new List<KsfEmployeeUnitMapping>();
        var existingMap = existing.ToDictionary(e => e.FID);

        foreach (var req in mappings)
        {
            if (req.Id.HasValue && req.Id.Value > 0 && existingMap.TryGetValue(req.Id.Value, out var entity))
            {
                entity.F员工ID = req.EmployeeId;
                entity.F经营单元ID = req.BusinessUnitId;
                entity.F分摊比例 = req.Ratio;
                entity.F生效起期 = req.EffectiveFrom;
                entity.F生效止期 = req.EffectiveTo;
            }
            else
            {
                _context.Set<KsfEmployeeUnitMapping>().Add(new KsfEmployeeUnitMapping
                {
                    FOrgId = orgId,
                    F员工ID = req.EmployeeId,
                    F经营单元ID = req.BusinessUnitId,
                    F分摊比例 = req.Ratio,
                    F生效起期 = req.EffectiveFrom,
                    F生效止期 = req.EffectiveTo,
                    F创建时间 = DateTime.Now
                });
            }
        }

        await _context.SaveChangesAsync();
        return ApiResult.Ok($"已保存 {mappings.Count} 条映射");
    }
}
