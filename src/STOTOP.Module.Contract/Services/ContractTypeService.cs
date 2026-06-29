using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;
using STOTOP.Module.Contract.Entities;
using STOTOP.Module.Contract.Services.Interfaces;

namespace STOTOP.Module.Contract.Services;

public class ContractTypeService : IContractTypeService
{
    private readonly IRepository<ConContractType> _repository;

    public ContractTypeService(IRepository<ConContractType> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<ContractTypeDto>> GetTypesAsync(ContractTypeQueryRequest request)
    {
        var query = _repository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(t => t.FName.Contains(keyword) || t.FCode.Contains(keyword));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(t => t.FStatus == request.Status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(t => t.FSortOrder)
            .ThenByDescending(t => t.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ContractTypeDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<List<ContractTypeDto>> GetAllEnabledTypesAsync()
    {
        var items = await _repository.Query()
            .Where(t => t.FStatus == 1)
            .OrderBy(t => t.FSortOrder)
            .ToListAsync();

        return items.Select(MapToDto).ToList();
    }

    public async Task<ContractTypeDto?> GetTypeByIdAsync(long id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ContractTypeDto> CreateTypeAsync(CreateContractTypeRequest request)
    {
        var exists = await _repository.Query().AnyAsync(t => t.FCode == request.Code);
        if (exists)
        {
            throw new InvalidOperationException($"合同类型编码 {request.Code} 已存在");
        }

        var entity = new ConContractType
        {
            FName = request.Name,
            FCode = request.Code,
            FDescription = request.Description,
            FSortOrder = request.SortOrder,
            FStatus = 1,
            FCreatedTime = DateTime.Now
        };

        await _repository.AddAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ContractTypeDto?> UpdateTypeAsync(long id, UpdateContractTypeRequest request)
    {
        var entity = await _repository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == id);

        if (entity == null) return null;

        var codeExists = await _repository.Query()
            .AnyAsync(t => t.FCode == request.Code && t.FID != id);
        if (codeExists)
        {
            throw new InvalidOperationException($"合同类型编码 {request.Code} 已存在");
        }

        entity.FName = request.Name;
        entity.FCode = request.Code;
        entity.FDescription = request.Description;
        entity.FSortOrder = request.SortOrder;
        entity.FUpdatedTime = DateTime.Now;

        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteTypeAsync(long id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null) return false;

        await _repository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> UpdateStatusAsync(long id, int status)
    {
        var entity = await _repository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == id);

        if (entity == null) return false;

        entity.FStatus = status;
        entity.FUpdatedTime = DateTime.Now;
        await _repository.UpdateAsync(entity);
        return true;
    }

    private static ContractTypeDto MapToDto(ConContractType entity)
    {
        return new ContractTypeDto
        {
            Id = entity.FID,
            Name = entity.FName,
            Code = entity.FCode,
            Description = entity.FDescription,
            SortOrder = entity.FSortOrder,
            Status = entity.FStatus,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime,
            UpdaterName = entity.FUpdaterName,
            UpdatedTime = entity.FUpdatedTime
        };
    }
}
