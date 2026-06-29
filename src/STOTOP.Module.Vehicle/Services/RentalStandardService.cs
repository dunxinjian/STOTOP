using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Entities;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle.Services;

public class RentalStandardService : IRentalStandardService
{
    private readonly IRepository<VehRentalStandard> _standardRepository;

    public RentalStandardService(IRepository<VehRentalStandard> standardRepository)
    {
        _standardRepository = standardRepository;
    }

    public async Task<PagedResult<RentalStandardListItemDto>> GetStandardsAsync(RentalStandardQueryRequest request)
    {
        var query = _standardRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(s => s.FName.Contains(keyword));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.FStatus == request.Status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(s => s.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<RentalStandardListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<RentalStandardDto?> GetStandardByIdAsync(long id)
    {
        var standard = await _standardRepository.GetByIdAsync(id);
        return standard == null ? null : MapToDto(standard);
    }

    public async Task<RentalStandardDto> CreateStandardAsync(CreateRentalStandardRequest request)
    {
        var standard = new VehRentalStandard
        {
            FUID = Guid.NewGuid().ToString("N"),
            FName = request.Name,
            FAmount = request.Amount,
            FChargeCycle = request.ChargeCycle,
            FEffectiveDate = request.EffectiveDate,
            FExpiryDate = request.ExpiryDate,
            FRemark = request.Remark,
            FStatus = 1,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _standardRepository.AddAsync(standard);

        return (await GetStandardByIdAsync(standard.FID))!;
    }

    public async Task<RentalStandardDto?> UpdateStandardAsync(long id, UpdateRentalStandardRequest request)
    {
        var standard = await _standardRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.FID == id);

        if (standard == null) return null;

        standard.FName = request.Name;
        standard.FAmount = request.Amount;
        standard.FChargeCycle = request.ChargeCycle;
        standard.FEffectiveDate = request.EffectiveDate;
        standard.FExpiryDate = request.ExpiryDate;
        standard.FRemark = request.Remark;
        standard.FUpdatedTime = DateTime.Now;

        await _standardRepository.UpdateAsync(standard);

        return await GetStandardByIdAsync(id);
    }

    public async Task<bool> UpdateStatusAsync(long id, int status)
    {
        var standard = await _standardRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.FID == id);

        if (standard == null) return false;

        standard.FStatus = status;
        standard.FUpdatedTime = DateTime.Now;

        await _standardRepository.UpdateAsync(standard);
        return true;
    }

    public async Task<List<RentalStandardListItemDto>> GetAllEnabledStandardsAsync()
    {
        var standards = await _standardRepository.Query()
            .Where(s => s.FStatus == 1)
            .OrderBy(s => s.FName)
            .ToListAsync();

        return standards.Select(MapToListItemDto).ToList();
    }

    #region Mapping

    private static RentalStandardDto MapToDto(VehRentalStandard entity)
    {
        return new RentalStandardDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            Name = entity.FName,
            Amount = entity.FAmount,
            ChargeCycle = entity.FChargeCycle,
            EffectiveDate = entity.FEffectiveDate,
            ExpiryDate = entity.FExpiryDate,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static RentalStandardListItemDto MapToListItemDto(VehRentalStandard entity)
    {
        return new RentalStandardListItemDto
        {
            Id = entity.FID,
            Name = entity.FName,
            Amount = entity.FAmount,
            ChargeCycle = entity.FChargeCycle,
            EffectiveDate = entity.FEffectiveDate,
            ExpiryDate = entity.FExpiryDate,
            Status = entity.FStatus
        };
    }

    #endregion
}
