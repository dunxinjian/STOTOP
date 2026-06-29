using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 新店铺自动发现（可被 Hangfire 调度）
/// </summary>
public class ShopAutoDiscoveryJob
{
    private readonly IRepository<ExpWaybill> _waybillRepository;
    private readonly IRepository<ExpShop> _shopRepository;

    public ShopAutoDiscoveryJob(
        IRepository<ExpWaybill> waybillRepository,
        IRepository<ExpShop> shopRepository)
    {
        _waybillRepository = waybillRepository;
        _shopRepository = shopRepository;
    }

    public async Task<ShopDiscoveryResult> ExecuteAsync()
    {
        // 1. 查询运单中所有 DISTINCT 店铺名称
        var waybillShopNames = await _waybillRepository.Query()
            .Select(w => w.FShopName)
            .Distinct()
            .ToListAsync();

        // 2. 查询已有店铺名称
        var existingShopNames = await _shopRepository.Query()
            .Select(s => s.FName)
            .ToListAsync();
        var existingSet = new HashSet<string>(existingShopNames, StringComparer.OrdinalIgnoreCase);

        // 3. 找出差集
        var newNames = waybillShopNames
            .Where(n => !string.IsNullOrWhiteSpace(n) && !existingSet.Contains(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 4. 为每个新店铺创建记录
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        for (int i = 0; i < newNames.Count; i++)
        {
            var shop = new ExpShop
            {
                FName = newNames[i],
                FIsAutoCreated = true,
                FNeedsAssignment = true,
                FStatus = 1,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            await _shopRepository.AddAsync(shop);
        }

        return new ShopDiscoveryResult
        {
            NewShopsCount = newNames.Count,
            ShopNames = newNames
        };
    }
}
