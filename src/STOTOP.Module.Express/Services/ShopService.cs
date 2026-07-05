using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class ShopService : IShopService
{
    private readonly IRepository<ExpShop> _repository;
    private readonly IRepository<ExpQuotationShop> _assignmentRepository;
    private readonly IRepository<ExpQuotation> _quotationRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly STOTOPDbContext _dbContext;

    public ShopService(
        IRepository<ExpShop> repository,
        IRepository<ExpQuotationShop> assignmentRepository,
        IRepository<ExpQuotation> quotationRepository,
        IHttpContextAccessor httpContextAccessor,
        STOTOPDbContext dbContext)
    {
        _repository = repository;
        _assignmentRepository = assignmentRepository;
        _quotationRepository = quotationRepository;
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    public async Task<PagedResult<ShopListItemDto>> GetListAsync(ShopQueryRequest request)
    {
        var query = _repository.Query();

        // 多网点视角过滤：通过 ExpQuotationShop → ExpQuotation.FOrgId 间接过滤
        var orgId = (long)(_httpContextAccessor.HttpContext?.Items["CurrentOrgId"] ?? 0L);
        if (orgId > 0)
        {
            var shopNames = await _assignmentRepository.Query()
                .Include(qs => qs.Quotation)
                .Where(qs => qs.Quotation.FOrgId == orgId)
                .Select(qs => qs.FShopName)
                .Distinct()
                .ToListAsync();
            query = query.Where(s => shopNames.Contains(s.FName));
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e => e.FName.Contains(keyword));
        }
        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);
        if (request.NeedsAssignment.HasValue)
            query = query.Where(e => e.FNeedsAssignment == request.NeedsAssignment.Value);
        if (!string.IsNullOrWhiteSpace(request.Platform))
            query = query.Where(e => e.FPlatform == request.Platform);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ShopListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ShopDto?> GetByNameAsync(string name)
    {
        var entity = await _repository.Query()
            .FirstOrDefaultAsync(e => e.FName == name);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ShopDto> CreateAsync(CreateShopRequest request)
    {
        var nameExists = await _repository.Query().AnyAsync(e => e.FName == request.Name);
        if (nameExists)
            throw new InvalidOperationException($"名称 '{request.Name}' 已存在");

        var entity = new ExpShop
        {
            FName = request.Name,
            FPlatform = request.Platform,
            FIsShared = request.IsShared,
            FContactName = request.ContactName,
            FContactPhone = request.ContactPhone,
            FStatus = request.Status,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        var result = await _repository.AddAsync(entity);
        return MapToDto(result);
    }

    public async Task<ShopDto?> UpdateAsync(string name, UpdateShopRequest request)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FName == name);
        if (entity == null) return null;

        entity.FPlatform = request.Platform;
        entity.FIsShared = request.IsShared;
        entity.FContactName = request.ContactName;
        entity.FContactPhone = request.ContactPhone;
        entity.FStatus = request.Status;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string name)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FName == name);
        if (entity == null) return false;
        // ExpShop 没有 FID，使用 EF 批量删除
        await _repository.Query().Where(e => e.FName == name).ExecuteDeleteAsync();
        return true;
    }

    public async Task<ShopAssignmentDto> AddAssignmentAsync(CreateShopAssignmentRequest request)
    {
        long quotationId;
        if (request.PricePlanId.HasValue && request.PricePlanId.Value > 0)
        {
            var plan = await _quotationRepository.GetByIdAsync(request.PricePlanId.Value);
            if (plan == null)
                throw new InvalidOperationException("指定的报价方案不存在");
            quotationId = request.PricePlanId.Value;
        }
        else
        {
            throw new InvalidOperationException("必须指定报价方案");
        }

        var entity = new ExpQuotationShop
        {
            FQuotationId = quotationId,
            FShopName = request.ShopName,
            FCreatedTime = DateTime.Now
        };
        var result = await _assignmentRepository.AddAsync(entity);
        return MapAssignmentToDto(result);
    }

    public async Task<bool> RemoveAssignmentAsync(long assignmentId)
    {
        var entity = await _assignmentRepository.GetByIdAsync(assignmentId);
        if (entity == null) return false;
        await _assignmentRepository.DeleteAsync(assignmentId);
        return true;
    }

    public async Task<List<QuotationShopDto>> GetShopsByQuotationIdAsync(long quotationId)
    {
        var items = await _dbContext.Set<ExpQuotationShop>()
            .Where(a => a.FQuotationId == quotationId)
            .OrderByDescending(a => a.FCreatedTime)
            .Select(a => new QuotationShopDto
            {
                Id = a.FID,
                QuotationId = a.FQuotationId,
                ShopName = a.FShopName,
                CreatedTime = a.FCreatedTime
            })
            .ToListAsync();
        return items;
    }

    public async Task<int> AddShopsToQuotationAsync(long quotationId, List<string> shopNames)
    {
        var plan = await _quotationRepository.GetByIdAsync(quotationId);
        if (plan == null)
            throw new InvalidOperationException("报价方案不存在");

        var normalizedShopNames = shopNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await _assignmentRepository.Query()
            .Where(a => a.FQuotationId == quotationId)
            .Select(a => a.FShopName)
            .ToListAsync();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var duplicated = normalizedShopNames
            .Where(n => existingSet.Contains(n))
            .ToList();
        if (duplicated.Count > 0)
            throw new InvalidOperationException($"店铺已关联当前报价：{string.Join(", ", duplicated)}");

        var toAdd = normalizedShopNames
            .Where(n => !existingSet.Contains(n))
            .ToList();

        await EnsureShopMastersAsync(toAdd);

        foreach (var name in toAdd)
        {
            await _assignmentRepository.AddAsync(new ExpQuotationShop
            {
                FQuotationId = quotationId,
                FShopName = name,
                FCreatedTime = DateTime.Now
            });
        }
        return toAdd.Count;
    }

    private async Task EnsureShopMastersAsync(List<string> shopNames)
    {
        if (shopNames.Count == 0) return;

        var existingShops = await _repository.Query()
            .Where(s => shopNames.Contains(s.FName))
            .ToListAsync();
        var existingNames = new HashSet<string>(
            existingShops.Select(s => s.FName),
            StringComparer.OrdinalIgnoreCase);

        foreach (var shop in existingShops.Where(s => s.FNeedsAssignment || s.FStatus == 0))
        {
            shop.FNeedsAssignment = false;
            shop.FStatus = 1;
            shop.FUpdatedTime = DateTime.Now;
            await _repository.UpdateAsync(shop);
        }

        foreach (var name in shopNames.Where(n => !existingNames.Contains(n)))
        {
            await _repository.AddAsync(new ExpShop
            {
                FName = name,
                FIsShared = false,
                FIsAutoCreated = false,
                FNeedsAssignment = false,
                FStatus = 1,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            });
        }
    }

    public async Task<bool> RemoveShopFromQuotationAsync(long quotationId, long shopId)
    {
        var entity = await _assignmentRepository.GetByIdAsync(shopId);
        if (entity == null || entity.FQuotationId != quotationId) return false;
        await _assignmentRepository.DeleteAsync(shopId);
        return true;
    }

    private static ShopDto MapToDto(ExpShop e) => new()
    {
        Name = e.FName,
        Platform = e.FPlatform,
        IsShared = e.FIsShared,
        IsAutoCreated = e.FIsAutoCreated,
        NeedsAssignment = e.FNeedsAssignment,
        ContactName = e.FContactName,
        ContactPhone = e.FContactPhone,
        Status = e.FStatus,
        Remark = e.FRemark,
        CreatedTime = e.FCreatedTime,
        UpdatedTime = e.FUpdatedTime,
        Assignments = null
    };

    private static ShopListItemDto MapToListItemDto(ExpShop e) => new()
    {
        Name = e.FName,
        Platform = e.FPlatform,
        IsShared = e.FIsShared,
        IsAutoCreated = e.FIsAutoCreated,
        NeedsAssignment = e.FNeedsAssignment,
        Status = e.FStatus,
        CreatedTime = e.FCreatedTime
    };

    private static ShopAssignmentDto MapAssignmentToDto(ExpQuotationShop e) => new()
    {
        Id = e.FID,
        ShopName = e.FShopName,
        ClientId = 0,
        PricePlanId = e.FQuotationId,
        EffectiveDate = DateOnly.FromDateTime(e.FCreatedTime),
        ExpiryDate = null,
        Remark = null,
        CreatedTime = e.FCreatedTime
    };

    public async Task<PagedResult<ShopAssignmentListItemDto>> GetAssignmentListAsync(ShopAssignmentQueryRequest request)
    {
        var query = _assignmentRepository.Query()
            .Include(a => a.Quotation)
            .Where(a => a.Quotation.FID == request.ClientId || a.FQuotationId == request.ClientId);

        if (!string.IsNullOrWhiteSpace(request.ShopName))
        {
            var shopName = request.ShopName.Trim();
            query = query.Where(a => a.FShopName.Contains(shopName));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new ShopAssignmentListItemDto
            {
                Id = a.FID,
                ShopName = a.FShopName,
                ClientId = 0,
                PricePlanId = a.FQuotationId,
                PricePlanName = a.Quotation.FPlanName,
                PricePlanStatus = a.Quotation.FStatus,
                EffectiveDate = a.Quotation.FEffectiveDate ?? DateOnly.FromDateTime(a.FCreatedTime),
                Remark = null,
                CreatedTime = a.FCreatedTime
            })
            .ToListAsync();

        return new PagedResult<ShopAssignmentListItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ShopAssignmentDto> CreateAssignmentAsync(CreateShopAssignmentRequest request)
    {
        long quotationId;

        if (request.PricePlanId.HasValue && request.PricePlanId.Value > 0)
        {
            // 验证报价方案是否存在
            var plan = await _quotationRepository.GetByIdAsync(request.PricePlanId.Value);
            if (plan == null)
                throw new InvalidOperationException("指定的报价方案不存在");
            quotationId = request.PricePlanId.Value;
        }
        else if (request.NewPricePlan != null)
        {
            // 新建草稿报价方案
            var orgId = (long)(_httpContextAccessor.HttpContext?.Items["CurrentOrgId"] ?? 0L);
            var newPlan = new ExpQuotation
            {
                FOrgId = orgId,
                FBrandCode = request.NewPricePlan.BrandCode,
                FPlanName = request.NewPricePlan.PlanName,
                FSettlementWeightStage = request.NewPricePlan.SettlementWeightStage,
                FStatus = 0, // 草稿
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            var createdPlan = await _quotationRepository.AddAsync(newPlan);
            quotationId = createdPlan.FID;
        }
        else
        {
            throw new InvalidOperationException("必须指定已有报价方案或提供新建方案信息");
        }

        var entity = new ExpQuotationShop
        {
            FQuotationId = quotationId,
            FShopName = request.ShopName,
            FCreatedTime = DateTime.Now
        };

        var result = await _assignmentRepository.AddAsync(entity);
        return MapAssignmentToDto(result);
    }

    public async Task<ShopAssignmentDto?> UpdateAssignmentAsync(long id, UpdateShopAssignmentRequest request)
    {
        var entity = await _assignmentRepository.GetByIdAsync(id);
        if (entity == null) return null;

        if (!string.IsNullOrWhiteSpace(request.ShopName))
            entity.FShopName = request.ShopName;

        if (request.PricePlanId.HasValue)
        {
            var plan = await _quotationRepository.GetByIdAsync(request.PricePlanId.Value);
            if (plan == null)
                throw new InvalidOperationException("指定的报价方案不存在");
            entity.FQuotationId = request.PricePlanId.Value;
        }

        await _assignmentRepository.UpdateAsync(entity);
        return MapAssignmentToDto(entity);
    }

    public async Task<bool> DeleteAssignmentAsync(long id)
    {
        var entity = await _assignmentRepository.GetByIdAsync(id);
        if (entity == null) return false;
        await _assignmentRepository.DeleteAsync(id);
        return true;
    }

    public async Task<List<ShopAssignmentBatchDto>> GetAssignmentBatchesAsync(long clientId)
    {
        var batches = await _assignmentRepository.Query()
            .Include(a => a.Quotation)
            .Where(a => a.FQuotationId == clientId || a.Quotation.FID == clientId)
            .GroupBy(a => a.FQuotationId)
            .Select(g => new ShopAssignmentBatchDto
            {
                PricePlanId = g.Key,
                PricePlanName = g.First().Quotation.FPlanName,
                PricePlanStatus = g.First().Quotation.FStatus,
                EffectiveDate = g.First().Quotation.FEffectiveDate ?? DateOnly.FromDateTime(g.Min(a => a.FCreatedTime)),
                ShopCount = g.Count(),
                AssignmentIds = g.Select(a => a.FID).ToList()
            })
            .OrderByDescending(b => b.EffectiveDate)
            .ToListAsync();

        return batches;
    }

    public async Task<List<BatchShopItemDto>> GetBatchShopsAsync(long clientId, long pricePlanId, DateTime effectiveDate)
    {
        var items = await _assignmentRepository.Query()
            .Where(a => a.FQuotationId == pricePlanId)
            .Select(a => new BatchShopItemDto
            {
                AssignmentId = a.FID,
                ShopName = a.FShopName,
                Remark = null,
                CreatedTime = a.FCreatedTime
            })
            .ToListAsync();

        return items;
    }

    public async Task<int> CreateBatchAssignmentAsync(CreateBatchAssignmentRequest request)
    {
        long quotationId;

        if (request.PricePlanId.HasValue && request.PricePlanId.Value > 0)
        {
            var plan = await _quotationRepository.GetByIdAsync(request.PricePlanId.Value);
            if (plan == null)
                throw new InvalidOperationException("指定的报价方案不存在");
            quotationId = request.PricePlanId.Value;
        }
        else if (request.NewPricePlan != null)
        {
            var orgId = (long)(_httpContextAccessor.HttpContext?.Items["CurrentOrgId"] ?? 0L);
            var newPlan = new ExpQuotation
            {
                FOrgId = orgId,
                FBrandCode = request.NewPricePlan.BrandCode,
                FPlanName = request.NewPricePlan.PlanName,
                FSettlementWeightStage = request.NewPricePlan.SettlementWeightStage,
                FStatus = 0,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            var createdPlan = await _quotationRepository.AddAsync(newPlan);
            quotationId = createdPlan.FID;
        }
        else
        {
            throw new InvalidOperationException("必须指定已有报价方案或提供新建方案信息");
        }

        if (request.ShopNames == null || request.ShopNames.Count == 0)
            throw new InvalidOperationException("店铺名称列表不能为空");

        var entities = request.ShopNames.Select(shopName => new ExpQuotationShop
        {
            FQuotationId = quotationId,
            FShopName = shopName,
            FCreatedTime = DateTime.Now
        }).ToList();

        foreach (var entity in entities)
        {
            await _assignmentRepository.AddAsync(entity);
        }

        return entities.Count;
    }

    private static readonly Dictionary<string, string> ClientTypeNameMap = new()
    {
        ["KH"] = "客户",
        ["DL"] = "代理",
        ["WD"] = "网点",
        ["YW"] = "业务员",
        ["CB"] = "承包区",
        ["YZ"] = "驿站",
    };

    public async Task<List<ShopConflictDto>> CheckShopConflictsAsync(long quotationId, List<string> shopNames)
    {
        if (shopNames == null || shopNames.Count == 0)
            return new List<ShopConflictDto>();

        // 获取当前报价，读取 FOrgId、FClientType、FBusinessObjectId。
        // 冲突检查本就是同组织内比对，故不再 IgnoreQueryFilters：让组织/租户全局过滤器生效，
        // 避免传入他组织 quotationId 时整段在他组织数据上运行(IDOR)。跨组织传入时 currentQuotation 为空直接返回。
        var currentQuotation = await _dbContext.Set<ExpQuotation>()
            .FirstOrDefaultAsync(q => q.FID == quotationId);

        if (currentQuotation == null)
            return new List<ShopConflictDto>();

        // 查询同组织、同 FClientType、FStatus=1 的报价，排除当前 FBusinessObjectId
        var quotations = await _dbContext.Set<ExpQuotation>()
            .Where(q => q.FOrgId == currentQuotation.FOrgId
                     && q.FClientType == currentQuotation.FClientType
                     && q.FStatus == 1
                     && q.FBusinessObjectId != currentQuotation.FBusinessObjectId)
            .Include(q => q.Shops)
            .ToListAsync();

        // 按 (FBusinessObjectId, FBrandCode) 分组，每组取 FEffectiveDate 最新的一条
        var latestQuotations = quotations
            .GroupBy(q => new { q.FBusinessObjectId, q.FBrandCode })
            .Select(g => g.OrderByDescending(q => q.FEffectiveDate).First())
            .ToList();

        // 检查目标店铺是否出现在这些最新报价的关联店铺中。
        // 必须 Trim + 忽略大小写：添加店铺与计费索引均按 OrdinalIgnoreCase 匹配，
        // 校验口径更严会漏报冲突，等真正计费时才撞车
        var shopNameSet = new HashSet<string>(
            shopNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
            StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<ShopConflictDto>();

        foreach (var q in latestQuotations)
        {
            var matchedShops = q.Shops
                .Where(s => !string.IsNullOrWhiteSpace(s.FShopName) && shopNameSet.Contains(s.FShopName.Trim()))
                .ToList();
            foreach (var shop in matchedShops)
            {
                conflicts.Add(new ShopConflictDto
                {
                    ShopName = shop.FShopName,
                    ClientType = q.FClientType ?? "",
                    ClientTypeName = ClientTypeNameMap.GetValueOrDefault(q.FClientType ?? "", q.FClientType ?? ""),
                    ClientId = q.FBusinessObjectId ?? "",
                    BrandCode = q.FBrandCode,
                    QuotationName = q.FPlanName,
                    EffectiveDate = q.FEffectiveDate
                });
            }
        }

        return conflicts;
    }
}
