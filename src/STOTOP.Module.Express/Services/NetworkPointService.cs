using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class NetworkPointService : INetworkPointService
{
    private readonly IRepository<ExpNetworkPoint> _repository;

    public NetworkPointService(IRepository<ExpNetworkPoint> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<NetworkPointDto>> GetListAsync(NetworkPointQueryRequest request)
    {
        var query = _repository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e =>
                e.FCode.Contains(keyword) ||
                (e.FShortName != null && e.FShortName.Contains(keyword)) ||
                (e.FFullName != null && e.FFullName.Contains(keyword)) ||
                (e.FAddress != null && e.FAddress.Contains(keyword)) ||
                (e.FManager != null && e.FManager.Contains(keyword)));
        }
        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<NetworkPointDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<NetworkPointDto?> GetByIdAsync(string code)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FCode == code);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<bool> CheckCodeExistsAsync(string code)
    {
        return await _repository.Query().AnyAsync(e => e.FCode == code);
    }

    public async Task<NetworkPointDto> CreateAsync(CreateNetworkPointRequest request)
    {
        // 编号必填校验
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("网点编号不能为空");

        // 编号唯一性校验
        var exists = await _repository.Query().AnyAsync(e => e.FCode == request.Code);
        if (exists)
            throw new InvalidOperationException($"网点编号 '{request.Code}' 已存在");

        var entity = new ExpNetworkPoint
        {
            FCode = request.Code,
            FShortName = request.ShortName,
            FParentPointCode = request.ParentPointCode,
            FOrgId = request.OrgId,
            FPointLevel = request.PointLevel,
            FIsPrimaryPoint = request.IsPrimaryPoint,
            FCoverageArea = request.CoverageArea,
            FDailyCapacity = request.DailyCapacity,
            FStorageArea = request.StorageArea,
            FBusinessHours = request.BusinessHours,
            FAddress = request.Address,
            FManager = request.Manager,
            FContactPhone = request.ContactPhone,
            FStatus = request.Status,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        var result = await _repository.AddAsync(entity);
        return MapToDto(result);
    }

    public async Task<NetworkPointDto?> UpdateAsync(string code, UpdateNetworkPointRequest request)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FCode == code);
        if (entity == null) return null;

        entity.FOrgId = request.OrgId;
        entity.FShortName = request.ShortName;
        entity.FParentPointCode = request.ParentPointCode;
        entity.FPointLevel = request.PointLevel;
        entity.FIsPrimaryPoint = request.IsPrimaryPoint;
        entity.FCoverageArea = request.CoverageArea;
        entity.FDailyCapacity = request.DailyCapacity;
        entity.FStorageArea = request.StorageArea;
        entity.FBusinessHours = request.BusinessHours;
        entity.FAddress = request.Address;
        entity.FManager = request.Manager;
        entity.FContactPhone = request.ContactPhone;
        entity.FStatus = request.Status;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string code)
    {
        var count = await _repository.Query()
            .Where(e => e.FCode == code)
            .ExecuteDeleteAsync();
        return count > 0;
    }

    private static NetworkPointDto MapToDto(ExpNetworkPoint e) => new()
    {
        Id = e.FCode,
        Code = e.FCode,
        ShortName = e.FShortName,
        FullName = e.FFullName,
        ParentPointCode = e.FParentPointCode,
        OrgId = e.FOrgId,
        PointLevel = e.FPointLevel,
        IsPrimaryPoint = e.FIsPrimaryPoint,
        CoverageArea = e.FCoverageArea,
        DailyCapacity = e.FDailyCapacity,
        StorageArea = e.FStorageArea,
        BusinessHours = e.FBusinessHours,
        Address = e.FAddress,
        Manager = e.FManager,
        ContactPhone = e.FContactPhone,
        Status = e.FStatus,
        Remark = e.FRemark,
        CreatedTime = e.FCreatedTime,
        UpdatedTime = e.FUpdatedTime
    };
}
