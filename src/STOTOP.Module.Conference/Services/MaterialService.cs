using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class MaterialService : IMaterialService
{
    private readonly STOTOPDbContext _dbContext;

    public MaterialService(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<MaterialListItemDto>> GetMaterialsAsync(int eventId, MaterialQueryRequest request)
    {
        var query = _dbContext.Set<ConfMaterial>()
            .Where(m => m.FEventId == eventId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(m => m.FName.Contains(keyword) || (m.FSupplier != null && m.FSupplier.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(m => m.FCategory == request.Category);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(m => m.FStatus == request.Status);

        if (!string.IsNullOrWhiteSpace(request.AcquisitionMethod))
            query = query.Where(m => m.FAcquisitionMethod == request.AcquisitionMethod);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(m => m.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<MaterialListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<MaterialDto?> GetMaterialByIdAsync(int id)
    {
        var material = await _dbContext.Set<ConfMaterial>()
            .Include(m => m.Schedule)
            .FirstOrDefaultAsync(m => m.FID == id);

        return material == null ? null : MapToDto(material);
    }

    public async Task<MaterialDto> CreateMaterialAsync(int eventId, CreateMaterialRequest request)
    {
        var material = new ConfMaterial
        {
            FEventId = eventId,
            FName = request.Name,
            FCategory = request.Category,
            FSpecification = request.Specification,
            FRequiredQuantity = request.RequiredQuantity,
            FUnit = request.Unit,
            FAcquisitionMethod = request.AcquisitionMethod,
            FUnitPrice = request.UnitPrice,
            FTotalPrice = request.UnitPrice * request.RequiredQuantity,
            FSupplier = request.Supplier,
            FSupplierContact = request.SupplierContact,
            FRequiredDate = request.RequiredDate,
            FResponsible = request.Responsible,
            FScheduleId = request.ScheduleId,
            FRemark = request.Remark,
            FStatus = "计划中",
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _dbContext.Set<ConfMaterial>().Add(material);
        await _dbContext.SaveChangesAsync();

        return (await GetMaterialByIdAsync((int)material.FID))!;
    }

    public async Task<MaterialDto?> UpdateMaterialAsync(int id, UpdateMaterialRequest request)
    {
        var material = await _dbContext.Set<ConfMaterial>()
            .AsTracking()
            .FirstOrDefaultAsync(m => m.FID == id);

        if (material == null) return null;

        material.FName = request.Name;
        material.FCategory = request.Category;
        material.FSpecification = request.Specification;
        material.FRequiredQuantity = request.RequiredQuantity;
        material.FUnit = request.Unit;
        material.FAcquisitionMethod = request.AcquisitionMethod;
        material.FUnitPrice = request.UnitPrice;
        material.FTotalPrice = request.UnitPrice * request.RequiredQuantity;
        material.FSupplier = request.Supplier;
        material.FSupplierContact = request.SupplierContact;
        material.FRequiredDate = request.RequiredDate;
        material.FResponsible = request.Responsible;
        material.FScheduleId = request.ScheduleId;
        material.FRemark = request.Remark;
        if (!string.IsNullOrWhiteSpace(request.Status))
            material.FStatus = request.Status;
        material.FUpdatedTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        return await GetMaterialByIdAsync(id);
    }

    public async Task<bool> DeleteMaterialAsync(int id)
    {
        var material = await _dbContext.Set<ConfMaterial>()
            .AsTracking()
            .FirstOrDefaultAsync(m => m.FID == id);

        if (material == null) return false;

        _dbContext.Set<ConfMaterial>().Remove(material);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<MaterialDto?> ReceiveMaterialAsync(int id, MaterialReceiveRequest request)
    {
        var material = await _dbContext.Set<ConfMaterial>()
            .AsTracking()
            .FirstOrDefaultAsync(m => m.FID == id);

        if (material == null) return null;

        material.FReceivedQuantity = request.ReceivedQuantity;
        material.FReceivedDate = request.ReceivedDate;
        material.FStatus = material.FReceivedQuantity >= material.FRequiredQuantity ? "已到位" : "部分到位";
        material.FUpdatedTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        return await GetMaterialByIdAsync(id);
    }

    public async Task<MaterialDto?> ReturnMaterialAsync(int id, MaterialReturnRequest request)
    {
        var material = await _dbContext.Set<ConfMaterial>()
            .AsTracking()
            .FirstOrDefaultAsync(m => m.FID == id);

        if (material == null) return null;

        material.FReturnDate = request.ReturnDate;
        material.FStatus = "已归还";
        if (!string.IsNullOrWhiteSpace(request.Remark))
            material.FRemark = request.Remark;
        material.FUpdatedTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        return await GetMaterialByIdAsync(id);
    }

    public async Task<MaterialSummaryDto> GetSummaryAsync(int eventId)
    {
        var materials = await _dbContext.Set<ConfMaterial>()
            .Where(m => m.FEventId == eventId)
            .ToListAsync();

        var summary = new MaterialSummaryDto
        {
            TotalCount = materials.Count,
            ReceivedCount = materials.Count(m => m.FStatus == "已到位"),
            PendingCount = materials.Count(m => m.FStatus != "已到位" && m.FStatus != "已归还"),
            TotalCost = materials.Sum(m => m.FTotalPrice),
            CategorySummaries = materials
                .GroupBy(m => m.FCategory ?? "未分类")
                .Select(g => new MaterialCategorySummary
                {
                    Category = g.Key,
                    Count = g.Count(),
                    TotalCost = g.Sum(m => m.FTotalPrice),
                    ReceivedCount = g.Count(m => m.FStatus == "已到位")
                }).ToList()
        };

        return summary;
    }

    public Task<byte[]> ExportMaterialsAsync(int eventId)
    {
        // TODO: 实现物品列表导出
        throw new NotImplementedException("物品导出功能待实现");
    }

    public async Task<List<MaterialListItemDto>> GetChecklistAsync(int eventId)
    {
        var materials = await _dbContext.Set<ConfMaterial>()
            .Where(m => m.FEventId == eventId && m.FStatus != "已到位" && m.FStatus != "已归还")
            .OrderBy(m => m.FRequiredDate)
            .ToListAsync();

        return materials.Select(MapToListItemDto).ToList();
    }

    #region Mapping

    private static MaterialDto MapToDto(ConfMaterial entity)
    {
        return new MaterialDto
        {
            Id = entity.FID,
            EventId = entity.FEventId,
            Name = entity.FName,
            Category = entity.FCategory,
            Specification = entity.FSpecification,
            RequiredQuantity = entity.FRequiredQuantity,
            ReceivedQuantity = entity.FReceivedQuantity,
            Unit = entity.FUnit,
            AcquisitionMethod = entity.FAcquisitionMethod,
            UnitPrice = entity.FUnitPrice,
            TotalPrice = entity.FTotalPrice,
            Supplier = entity.FSupplier,
            SupplierContact = entity.FSupplierContact,
            RequiredDate = entity.FRequiredDate,
            ReceivedDate = entity.FReceivedDate,
            ReturnDate = entity.FReturnDate,
            Status = entity.FStatus,
            Responsible = entity.FResponsible,
            ScheduleId = entity.FScheduleId,
            ScheduleTitle = entity.Schedule?.FTitle,
            Remark = entity.FRemark,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static MaterialListItemDto MapToListItemDto(ConfMaterial entity)
    {
        return new MaterialListItemDto
        {
            Id = entity.FID,
            EventId = entity.FEventId,
            Name = entity.FName,
            Category = entity.FCategory,
            RequiredQuantity = entity.FRequiredQuantity,
            ReceivedQuantity = entity.FReceivedQuantity,
            Unit = entity.FUnit,
            AcquisitionMethod = entity.FAcquisitionMethod,
            TotalPrice = entity.FTotalPrice,
            Status = entity.FStatus,
            Responsible = entity.FResponsible,
            RequiredDate = entity.FRequiredDate
        };
    }

    #endregion
}
