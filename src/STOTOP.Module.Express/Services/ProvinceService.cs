using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class ProvinceService : IProvinceService
{
    private readonly IRepository<ExpProvince> _repository;

    public ProvinceService(IRepository<ExpProvince> repository)
    {
        _repository = repository;
    }

    public async Task<List<ProvinceListItemDto>> GetAllAsync()
    {
        var items = await _repository.Query()
            .OrderBy(e => e.FCode)
            .ToListAsync();
        return items.Select(MapToListItemDto).ToList();
    }

    public async Task<ProvinceDto?> GetByIdAsync(int id)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FID == id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ProvinceDto> CreateAsync(CreateProvinceRequest request)
    {
        // 检查编码唯一性
        var exists = await _repository.Query().AnyAsync(e => e.FCode == request.Code);
        if (exists)
            throw new InvalidOperationException($"省份编码 '{request.Code}' 已存在");

        var entity = new ExpProvince
        {
            FCode = request.Code,
            FName = request.Name,
            FShortName = request.ShortName,
            FRegion = request.Region,
            FIsRemote = request.IsRemote
        };
        var result = await _repository.AddAsync(entity);
        return MapToDto(result);
    }

    public async Task<ProvinceDto?> UpdateAsync(int id, UpdateProvinceRequest request)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FID == id);
        if (entity == null) return null;

        // 检查编码唯一性（排除自身）
        var codeExists = await _repository.Query().AnyAsync(e => e.FCode == request.Code && e.FID != id);
        if (codeExists)
            throw new InvalidOperationException($"省份编码 '{request.Code}' 已存在");

        entity.FCode = request.Code;
        entity.FName = request.Name;
        entity.FShortName = request.ShortName;
        entity.FRegion = request.Region;
        entity.FIsRemote = request.IsRemote;
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FID == id);
        if (entity == null) return false;
        await _repository.DeleteAsync(entity.FID);
        return true;
    }

    public async Task<List<string>> GetRegionsAsync()
    {
        return await _repository.Query()
            .Where(e => e.FRegion != null && e.FRegion != "")
            .Select(e => e.FRegion!)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();
    }

    public async Task<int> RenameRegionAsync(string oldName, string newName)
    {
        var provinces = await _repository.Query()
            .Where(e => e.FRegion == oldName)
            .ToListAsync();
        foreach (var p in provinces)
        {
            p.FRegion = newName;
            await _repository.UpdateAsync(p);
        }
        return provinces.Count;
    }

    private static ProvinceDto MapToDto(ExpProvince e) => new()
    {
        Id = e.FID,
        Code = e.FCode,
        Name = e.FName,
        ShortName = e.FShortName,
        Region = e.FRegion,
        IsRemote = e.FIsRemote
    };

    private static ProvinceListItemDto MapToListItemDto(ExpProvince e) => new()
    {
        Id = e.FID,
        Code = e.FCode,
        Name = e.FName,
        ShortName = e.FShortName,
        Region = e.FRegion,
        IsRemote = e.FIsRemote
    };
}
