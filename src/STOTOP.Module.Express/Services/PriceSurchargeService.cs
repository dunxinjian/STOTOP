using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 附加费服务（基于作用域架构）
/// </summary>
public class PriceSurchargeService : IPriceSurchargeService
{
    private readonly IRepository<ExpPriceSurcharge> _surchargeRepo;
    private readonly IRepository<ExpPriceSurchargeItem> _itemRepo;
    private readonly IRepository<ExpPriceSurchargeItemDest> _destRepo;
    private readonly IRepository<ExpPriceSurchargeScope> _scopeRepo;
    private readonly STOTOPDbContext _dbContext;

    public PriceSurchargeService(
        IRepository<ExpPriceSurcharge> surchargeRepo,
        IRepository<ExpPriceSurchargeItem> itemRepo,
        IRepository<ExpPriceSurchargeItemDest> destRepo,
        IRepository<ExpPriceSurchargeScope> scopeRepo,
        STOTOPDbContext dbContext)
    {
        _surchargeRepo = surchargeRepo;
        _itemRepo = itemRepo;
        _destRepo = destRepo;
        _scopeRepo = scopeRepo;
        _dbContext = dbContext;
    }

    public async Task<PagedResult<PriceSurchargeListItemDto>> GetListAsync(PriceSurchargeQueryRequest request)
    {
        var query = _surchargeRepo.Query()
            .Include(e => e.Scopes)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(e => e.FBrandCode == request.BrandCode);
        if (!string.IsNullOrWhiteSpace(request.NetworkPointCode))
            query = query.Where(e => e.FNetworkPointCode == request.NetworkPointCode);
        if (request.Scope.HasValue)
            query = query.Where(e => e.FScope == request.Scope.Value);
        if (!string.IsNullOrWhiteSpace(request.SurchargeType))
            query = query.Where(e => e.FSurchargeType.ToString() == request.SurchargeType);
        if (request.IsActive.HasValue)
            query = query.Where(e => e.FIsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e => e.FName.Contains(keyword));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new PriceSurchargeListItemDto
            {
                Id = e.FID,
                SurchargeType = e.FSurchargeType.ToString(),
                Scope = e.FScope,
                BrandCode = e.FBrandCode,
                NetworkPointCode = e.FNetworkPointCode,
                Name = e.FName,
                EffectiveDate = e.FEffectiveDate,
                IsActive = e.FIsActive,
                CreatedTime = e.FCreatedTime,
                UpdatedTime = e.FUpdatedTime,
                Scopes = e.Scopes.Select(s => new SurchargeScopeDto
                {
                    LinkedType = s.FLinkedType,
                    LinkedId = s.FLinkedId
                }).ToList()
            })
            .ToListAsync();

        return new PagedResult<PriceSurchargeListItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<PriceSurchargeDto?> GetByIdAsync(long id)
    {
        var entity = await _surchargeRepo.Query()
            .Include(e => e.Scopes)
            .Include(e => e.Items)
                .ThenInclude(i => i.Destinations)
            .FirstOrDefaultAsync(e => e.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<PriceSurchargeDto> CreateAsync(CreatePriceSurchargeRequest request)
    {
        // 主表 + 作用域 + 配置项 + 目的地多次提交需原子化，避免中途失败留下半截配置
        await using var tx = await _dbContext.Database.BeginTransactionAsync();
        try
        {
        var entity = new ExpPriceSurcharge
        {
            FBrandCode = request.BrandCode,
            FNetworkPointCode = request.NetworkPointCode,
            FName = request.Name ?? string.Empty,
            FSurchargeType = int.TryParse(request.SurchargeType, out var st) ? st : 0,
            FScope = request.Scope,
            // 默认取当天零点：生效判断是 EffectiveDate <= 运单日期（纯日期），
            // 带时分秒会让创建当天的运单全部判定"未生效"
            FEffectiveDate = request.EffectiveDate ?? DateTime.Today,
            FIsActive = request.IsActive,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        var result = await _surchargeRepo.AddAsync(entity);

        // 创建作用域关联记录（scope=0 全局时不创建）
        if (request.Scope > 0 && request.Scopes is { Count: > 0 })
        {
            foreach (var scopeInput in request.Scopes)
            {
                var scope = new ExpPriceSurchargeScope
                {
                    FSurchargeId = result.FID,
                    FLinkedType = scopeInput.LinkedType,
                    FLinkedId = scopeInput.LinkedId
                };
                await _scopeRepo.AddAsync(scope);
            }
        }

        // 创建配置项和目的地
        foreach (var itemInput in request.Items)
        {
            var item = new ExpPriceSurchargeItem
            {
                FSurchargeId = result.FID,
                FCalcMethod = itemInput.CalcMethod,
                FWeightRoundingMethod = itemInput.WeightRoundingMethod,
                FWeightFrom = itemInput.WeightFrom,
                FWeightTo = itemInput.WeightTo,
                FWeightType = itemInput.WeightType,
                FDailyVolumeFrom = itemInput.DailyVolumeFrom,
                FDailyVolumeTo = itemInput.DailyVolumeTo,
                FAmount = itemInput.Amount,
                FSortOrder = itemInput.SortOrder
            };
            var savedItem = await _itemRepo.AddAsync(item);

            foreach (var destInput in itemInput.Destinations)
            {
                var dest = new ExpPriceSurchargeItemDest
                {
                    FSurchargeItemId = savedItem.FID,
                    FDestType = destInput.DestType,
                    FProvinceId = destInput.ProvinceId,
                    FCityName = destInput.CityName
                };
                await _destRepo.AddAsync(dest);
            }
        }

        await tx.CommitAsync();
        return (await GetByIdAsync(result.FID))!;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<PriceSurchargeDto?> UpdateAsync(long id, UpdatePriceSurchargeRequest request)
    {
        var entity = await _surchargeRepo.Query()
            .Include(e => e.Scopes)
            .Include(e => e.Items)
                .ThenInclude(i => i.Destinations)
            .FirstOrDefaultAsync(e => e.FID == id);

        if (entity == null) return null;

        // 删除旧项→改主表→重建项需原子化
        await using var tx = await _dbContext.Database.BeginTransactionAsync();
        try
        {
        // 删除旧的目的地和配置项
        foreach (var item in entity.Items.ToList())
        {
            foreach (var dest in item.Destinations.ToList())
                await _destRepo.DeleteAsync(dest.FID);
            await _itemRepo.DeleteAsync(item.FID);
        }

        // 删除旧的作用域关联记录
        foreach (var scope in entity.Scopes.ToList())
            await _scopeRepo.DeleteAsync(scope.Id);

        // 更新基本信息（Name/IsActive/Remark 为 null 表示不修改，见 UpdatePriceSurchargeRequest 注释）
        entity.FName = request.Name ?? entity.FName;
        entity.FSurchargeType = int.TryParse(request.SurchargeType, out var st) ? st : 0;
        entity.FScope = request.Scope;
        entity.FBrandCode = request.BrandCode;
        entity.FNetworkPointCode = request.NetworkPointCode;
        entity.FEffectiveDate = request.EffectiveDate ?? entity.FEffectiveDate;
        entity.FIsActive = request.IsActive ?? entity.FIsActive;
        entity.FRemark = request.Remark ?? entity.FRemark;
        entity.FUpdatedTime = DateTime.Now;

        await _surchargeRepo.UpdateAsync(entity);

        // 创建新的作用域关联记录
        if (request.Scope > 0 && request.Scopes is { Count: > 0 })
        {
            foreach (var scopeInput in request.Scopes)
            {
                var scope = new ExpPriceSurchargeScope
                {
                    FSurchargeId = id,
                    FLinkedType = scopeInput.LinkedType,
                    FLinkedId = scopeInput.LinkedId
                };
                await _scopeRepo.AddAsync(scope);
            }
        }

        // 重新创建配置项和目的地
        foreach (var itemInput in request.Items)
        {
            var item = new ExpPriceSurchargeItem
            {
                FSurchargeId = id,
                FCalcMethod = itemInput.CalcMethod,
                FWeightRoundingMethod = itemInput.WeightRoundingMethod,
                FWeightFrom = itemInput.WeightFrom,
                FWeightTo = itemInput.WeightTo,
                FWeightType = itemInput.WeightType,
                FDailyVolumeFrom = itemInput.DailyVolumeFrom,
                FDailyVolumeTo = itemInput.DailyVolumeTo,
                FAmount = itemInput.Amount,
                FSortOrder = itemInput.SortOrder
            };
            var savedItem = await _itemRepo.AddAsync(item);

            foreach (var destInput in itemInput.Destinations)
            {
                var dest = new ExpPriceSurchargeItemDest
                {
                    FSurchargeItemId = savedItem.FID,
                    FDestType = destInput.DestType,
                    FProvinceId = destInput.ProvinceId,
                    FCityName = destInput.CityName
                };
                await _destRepo.AddAsync(dest);
            }
        }

        await tx.CommitAsync();
        return (await GetByIdAsync(id))!;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _surchargeRepo.Query()
            .Include(e => e.Scopes)
            .Include(e => e.Items)
                .ThenInclude(i => i.Destinations)
            .FirstOrDefaultAsync(e => e.FID == id);

        if (entity == null) return false;

        // 级联删除需原子化
        await using var tx = await _dbContext.Database.BeginTransactionAsync();
        try
        {
        // 删除作用域关联
        foreach (var scope in entity.Scopes.ToList())
            await _scopeRepo.DeleteAsync(scope.Id);

        // 删除配置项和目的地
        foreach (var item in entity.Items.ToList())
        {
            foreach (var dest in item.Destinations.ToList())
                await _destRepo.DeleteAsync(dest.FID);
            await _itemRepo.DeleteAsync(item.FID);
        }

        await _surchargeRepo.DeleteAsync(id);
        await tx.CommitAsync();
        return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> ToggleActiveAsync(long id)
    {
        var entity = await _surchargeRepo.Query().FirstOrDefaultAsync(e => e.FID == id);
        if (entity == null) return false;

        entity.FIsActive = !entity.FIsActive;
        entity.FUpdatedTime = DateTime.Now;
        await _surchargeRepo.UpdateAsync(entity);
        return true;
    }

    private static PriceSurchargeDto MapToDto(ExpPriceSurcharge e) => new()
    {
        Id = e.FID,
        SurchargeType = e.FSurchargeType.ToString(),
        Scope = e.FScope,
        BrandCode = e.FBrandCode,
        NetworkPointCode = e.FNetworkPointCode,
        Name = e.FName,
        EffectiveDate = e.FEffectiveDate,
        IsActive = e.FIsActive,
        Remark = e.FRemark,
        CreatedTime = e.FCreatedTime,
        UpdatedTime = e.FUpdatedTime,
        Scopes = e.Scopes.Select(s => new SurchargeScopeDto
        {
            LinkedType = s.FLinkedType,
            LinkedId = s.FLinkedId
        }).ToList(),
        Items = e.Items.Select(i => new SurchargeItemDto
        {
            Id = i.FID,
            CalcMethod = i.FCalcMethod,
            WeightRoundingMethod = i.FWeightRoundingMethod,
            WeightFrom = i.FWeightFrom,
            WeightTo = i.FWeightTo,
            WeightType = i.FWeightType,
            DailyVolumeFrom = i.FDailyVolumeFrom,
            DailyVolumeTo = i.FDailyVolumeTo,
            Amount = i.FAmount,
            SortOrder = i.FSortOrder,
            Destinations = i.Destinations.Select(d => new SurchargeDestDto
            {
                Id = d.FID,
                DestType = d.FDestType,
                ProvinceId = d.FProvinceId,
                CityName = d.FCityName
            }).ToList()
        }).ToList()
    };
}
