using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class WaybillService : IWaybillService
{
    private readonly IRepository<ExpWaybill> _repository;
    private readonly IRepository<ExpBrand> _brandRepository;
    private readonly IRepository<ExpQuotation> _quotationRepository;
    private readonly IRepository<ExpProvince> _provinceRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public WaybillService(
        IRepository<ExpWaybill> repository,
        IRepository<ExpBrand> brandRepository,
        IRepository<ExpQuotation> quotationRepository,
        IRepository<ExpProvince> provinceRepository,
        IHttpContextAccessor httpContextAccessor)
    {
        _repository = repository;
        _brandRepository = brandRepository;
        _quotationRepository = quotationRepository;
        _provinceRepository = provinceRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PagedResult<WaybillListItemDto>> GetListAsync(WaybillQueryRequest request)
    {
        var query = _repository.Query();

        // 多网点视角过滤已由全局查询过滤器（IOrgScoped.FOrgId）自动处理

        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(e => e.FBrandCode == request.BrandCode);
        if (!string.IsNullOrWhiteSpace(request.ShopName))
            query = query.Where(e => e.FShopName.Contains(request.ShopName.Trim()));
        if (!string.IsNullOrWhiteSpace(request.ClientId))
            query = query.Where(e => e.FBusinessObjectId == request.ClientId);
        if (request.ReceiverProvinceId.HasValue)
            query = query.Where(e => e.FReceiverProvinceId == request.ReceiverProvinceId.Value);
        if (request.DateFrom.HasValue)
            query = query.Where(e => e.FWaybillDate >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(e => e.FWaybillDate <= request.DateTo.Value);
        if (request.BillingStatus.HasValue)
            query = query.Where(e => e.FBillingStatus == request.BillingStatus.Value);
        if (!string.IsNullOrWhiteSpace(request.WaybillNo))
            query = query.Where(e => e.FWaybillNo.Contains(request.WaybillNo.Trim()));

        var total = await query.CountAsync();

        var brands = _brandRepository.Query();
        var clients = _quotationRepository.Query();
        var provinces = _provinceRepository.Query();

        var items = await query
            .OrderByDescending(e => e.FWaybillDate)
            .ThenByDescending(e => e.FID)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Join(brands, w => w.FBrandCode, b => b.FCode, (w, b) => new { w, BrandName = b.FName })
            .Select(x => new WaybillListItemDto
            {
                Id = x.w.FID,
                WaybillNo = x.w.FWaybillNo,
                BrandName = x.BrandName,
                ShopName = x.w.FShopName,
                ClientName = clients.Where(c => c.FBusinessObjectId == x.w.FBusinessObjectId).Select(c => c.FPlanName).FirstOrDefault(),
                ProvinceName = provinces.Where(p => p.FID == x.w.FReceiverProvinceId).Select(p => p.FName).FirstOrDefault(),
                PickupWeight = x.w.FPickupWeight,
                ActualWeight = x.w.FActualWeight,
                BillableWeight = x.w.FBillableWeight,
                WaybillDate = x.w.FWaybillDate,
                BillingStatus = x.w.FBillingStatus
            })
            .ToListAsync();

        return new PagedResult<WaybillListItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<WaybillDto?> GetByIdAsync(long id)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FID == id);
        if (entity == null) return null;
        return await MapToDtoAsync(entity);
    }

    public async Task<WaybillDto?> GetByWaybillNoAsync(string waybillNo, string brandCode)
    {
        var entity = await _repository.Query()
            .FirstOrDefaultAsync(e => e.FWaybillNo == waybillNo && e.FBrandCode == brandCode);
        if (entity == null) return null;
        return await MapToDtoAsync(entity);
    }

    private async Task<WaybillDto> MapToDtoAsync(ExpWaybill e)
    {
        var brandName = await _brandRepository.Query()
            .Where(b => b.FCode == e.FBrandCode).Select(b => b.FName).FirstOrDefaultAsync();
        var clientName = e.FBusinessObjectId != null
            ? await _quotationRepository.Query()
                .Where(c => c.FBusinessObjectId == e.FBusinessObjectId).Select(c => c.FPlanName).FirstOrDefaultAsync()
            : null;
        var provinceName = e.FReceiverProvinceId.HasValue
            ? await _provinceRepository.Query()
                .Where(p => p.FID == e.FReceiverProvinceId.Value).Select(p => p.FName).FirstOrDefaultAsync()
            : null;

        return new WaybillDto
        {
            Id = e.FID,
            WaybillNo = e.FWaybillNo,
            BrandCode = e.FBrandCode,
            BrandName = brandName,
            ShopName = e.FShopName,
            ClientId = e.FBusinessObjectId,
            ClientName = clientName,
            SenderProvince = e.FSenderProvince,
            ReceiverProvinceId = e.FReceiverProvinceId,
            ProvinceName = provinceName,
            PickupWeight = e.FPickupWeight,
            TransitWeight = e.FTransitWeight,
            DeliveryWeight = e.FDeliveryWeight,
            BagWeight = e.FBagWeight,
            BubbleWeight = e.FBubbleWeight,
            HqWeight = e.FHqWeight,
            IsDirectDelivery = e.FIsDirectDelivery,
            ActualWeight = e.FActualWeight,
            Length = e.FLength,
            Width = e.FWidth,
            Height = e.FHeight,
            VolumetricWeight = e.FVolumetricWeight,
            BillableWeight = e.FBillableWeight,
            DeclaredValue = e.FDeclaredValue,
            WaybillDate = e.FWaybillDate,
            ImportBatchId = e.FImportBatchId,
            ClientAlias = e.FClientAlias,
            BillingStatus = e.FBillingStatus,
            CreatedTime = e.FCreatedTime
        };
    }
}
