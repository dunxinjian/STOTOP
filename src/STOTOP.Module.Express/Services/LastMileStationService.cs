using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Express.Services;

public class LastMileStationService : ILastMileStationService
{
    private readonly IRepository<ExpLastMileStation> _repository;
    private readonly IRepository<SysOrganization> _orgRepository;
    private readonly IRepository<ExpQuotation> _quotationRepository;

    public LastMileStationService(
        IRepository<ExpLastMileStation> repository,
        IRepository<SysOrganization> orgRepository,
        IRepository<ExpQuotation> quotationRepository)
    {
        _repository = repository;
        _orgRepository = orgRepository;
        _quotationRepository = quotationRepository;
    }

    public async Task<PagedResult<LastMileStationDto>> GetListAsync(LastMileStationQueryRequest request)
    {
        var query = _repository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e =>
                e.FCode.Contains(keyword) ||
                (e.FName != null && e.FName.Contains(keyword)) ||
                (e.FContactName != null && e.FContactName.Contains(keyword)));
        }
        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);
        if (request.StationType.HasValue)
            query = query.Where(e => e.FStationType == request.StationType.Value);

        var total = await query.CountAsync();

        var joinQuery = from station in query
                        join q in _quotationRepository.Query().Where(q => q.FClientType == "YZ")
                            on station.FCode equals q.FBusinessObjectId into gc
                        from q in gc.DefaultIfEmpty()
                        orderby station.FCreatedTime descending
                        select new { Station = station, ServiceObjectId = q != null ? (long?)q.FID : null };

        var items = await joinQuery
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<LastMileStationDto>
        {
            Items = items.Select(x => { var dto = MapToDto(x.Station); dto.ServiceObjectId = x.ServiceObjectId; return dto; }).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<LastMileStationDto?> GetByIdAsync(string code)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FCode == code);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<LastMileStationDto> CreateAsync(CreateLastMileStationRequest request)
    {
        ValidateStationType(request.StationType, request.OrgId, request.Code, request.Name);

        var entity = new ExpLastMileStation
        {
            FCode = request.Code ?? string.Empty,
            FStationType = request.StationType,
            FOrgId = request.OrgId,
            FName = request.Name,
            FAddress = request.Address,
            FBusinessHours = request.BusinessHours,
            FDailyVolume = request.DailyVolume,
            FShelfCount = request.ShelfCount,
            FArea = request.Area,
            FContactName = request.ContactName,
            FContactPhone = request.ContactPhone,
            FCooperationStartDate = request.CooperationStartDate,
            FStatus = request.Status,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        // 直营模式：从组织获取编码和名称
        if (request.StationType == 1 && request.OrgId.HasValue)
        {
            var org = await _orgRepository.GetByIdAsync(request.OrgId.Value);
            if (org != null)
            {
                entity.FCode = org.FCode;
                entity.FName = org.FName;
            }
        }

        // 检查编码唯一性
        if (!string.IsNullOrWhiteSpace(entity.FCode))
        {
            var exists = await _repository.Query().AnyAsync(e => e.FCode == entity.FCode);
            if (exists)
                throw new InvalidOperationException($"末端驿站编号 '{entity.FCode}' 已存在");
        }

        var result = await _repository.AddAsync(entity);
        return MapToDto(result);
    }

    public async Task<LastMileStationDto?> UpdateAsync(string code, UpdateLastMileStationRequest request)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FCode == code);
        if (entity == null) return null;

        ValidateStationType(request.StationType, request.OrgId, request.Code, request.Name);

        entity.FStationType = request.StationType;
        entity.FOrgId = request.OrgId;
        entity.FName = request.Name;
        entity.FAddress = request.Address;
        entity.FBusinessHours = request.BusinessHours;
        entity.FDailyVolume = request.DailyVolume;
        entity.FShelfCount = request.ShelfCount;
        entity.FArea = request.Area;
        entity.FContactName = request.ContactName;
        entity.FContactPhone = request.ContactPhone;
        entity.FCooperationStartDate = request.CooperationStartDate;
        entity.FStatus = request.Status;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        // 直营模式：从组织获取编码和名称
        if (request.StationType == 1 && request.OrgId.HasValue)
        {
            var org = await _orgRepository.GetByIdAsync(request.OrgId.Value);
            if (org != null)
            {
                entity.FName = org.FName;
            }
        }

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

    /// <summary>
    /// 校验驿站类型：直营必须有组织ID，合作必须有编码和名称
    /// </summary>
    private static void ValidateStationType(int stationType, long? orgId, string? code, string? name)
    {
        if (stationType == 1) // 直营
        {
            if (!orgId.HasValue || orgId.Value <= 0)
                throw new InvalidOperationException("直营驿站必须指定组织ID");
        }
        else if (stationType == 2) // 合作
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("合作驿站必须填写编码");
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("合作驿站必须填写名称");
        }
        else
        {
            throw new InvalidOperationException("驿站类型只能为 1(直营) 或 2(合作)");
        }
    }

    private static LastMileStationDto MapToDto(ExpLastMileStation e) => new()
    {
        Id = e.FCode,
        StationType = e.FStationType,
        OrgId = e.FOrgId,
        Code = e.FCode,
        Name = e.FName,
        Address = e.FAddress,
        BusinessHours = e.FBusinessHours,
        DailyVolume = e.FDailyVolume,
        ShelfCount = e.FShelfCount,
        Area = e.FArea,
        ContactName = e.FContactName,
        ContactPhone = e.FContactPhone,
        CooperationStartDate = e.FCooperationStartDate,
        Status = e.FStatus,
        Remark = e.FRemark,
        CreatedTime = e.FCreatedTime,
        UpdatedTime = e.FUpdatedTime
    };
}
