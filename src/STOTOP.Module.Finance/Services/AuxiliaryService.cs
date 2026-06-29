using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class AuxiliaryService : IAuxiliaryService
{
    private readonly IRepository<FinAuxiliaryType> _typeRepository;
    private readonly IRepository<FinAuxiliaryItem> _itemRepository;
    private readonly IRepository<FinVoucherEntry> _voucherEntryRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly STOTOPDbContext _dbContext;
    private readonly ICodeRuleService _codeRuleService;

    public AuxiliaryService(
        IRepository<FinAuxiliaryType> typeRepository,
        IRepository<FinAuxiliaryItem> itemRepository,
        IRepository<FinVoucherEntry> voucherEntryRepository,
        IHttpContextAccessor httpContextAccessor,
        STOTOPDbContext dbContext,
        ICodeRuleService codeRuleService)
    {
        _typeRepository = typeRepository;
        _itemRepository = itemRepository;
        _voucherEntryRepository = voucherEntryRepository;
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _codeRuleService = codeRuleService;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    #region Auxiliary Type

    public async Task<List<AuxiliaryTypeDto>> GetTypesAsync()
    {
        var types = await _typeRepository.Query()
            .Where(t => t.FStatus == 1)
            .OrderBy(t => t.FName)
            .ToListAsync();
        
        return types.Select(MapTypeToDto).ToList();
    }

    #endregion

    private static AuxiliaryTypeDto MapTypeToDto(FinAuxiliaryType type)
    {
        return new AuxiliaryTypeDto
        {
            Id = type.FID,
            Name = type.FName,
            Status = type.FStatus,
            Scope = type.FScope
        };
    }

    private static AuxiliaryItemDto MapItemToDto(FinAuxiliaryItem item)
    {
        return new AuxiliaryItemDto
        {
            Id = item.FID,
            TypeName = item.FAuxType ?? string.Empty,
            Code = item.FCode,
            Name = item.FName,
            AccountSetId = item.FAccountSetId,
            AuxType = item.FAuxType,
            ShortName = item.FShortName,
            Contact = item.FContact,
            Phone = item.FPhone,
            Address = item.FAddress,
            Remark = item.FRemark,
            EnableStatus = item.FEnableStatus,
            SourceType = item.FSourceType,
            SourceId = item.FSourceId
        };
    }

    #region 账套维度辅助核算新接口

    public async Task<List<AuxiliaryItemDto>> GetItemsByAccountSetAsync(AuxiliaryItemQueryRequest request)
    {
        var currentOrgId = GetCurrentOrgId();
        // 使用 IgnoreQueryFilters 绕过 IOrgScoped 全局过滤器，
        // 因为全局类型(express_brand/business_direction)的 FOrgId=0 需要跨组织可见，
        // 而组织隔离逻辑已在下方按 typeScope 显式处理
        var query = _dbContext.Set<FinAuxiliaryItem>().IgnoreQueryFilters().AsQueryable()
            .Where(i => i.FEnableStatus == 1);

        if (!string.IsNullOrEmpty(request.AuxType))
        {
            // 先按类型过滤
            query = query.Where(i => i.FAuxType == request.AuxType);

            // 查询该类型的 scope，从类型表读取
            var typeScope = await _typeRepository.Query()
                .Where(t => t.FName == request.AuxType)
                .Select(t => t.FScope)
                .FirstOrDefaultAsync() ?? "org_scoped";

            // 只有 org_scoped 类型才需要账套+组织过滤
            if (typeScope != "global")
            {
                query = query.Where(i =>
                    i.FAccountSetId == request.AccountSetId &&
                    (currentOrgId == 0 || i.FOrgId == currentOrgId || i.FOrgId == 0));
            }
        }
        else
        {
            // 未指定类型：获取所有 global 类型名，混合查询
            var globalTypeNames = await _typeRepository.Query()
                .Where(t => t.FScope == "global" && t.FStatus == 1)
                .Select(t => t.FName)
                .ToListAsync();

            query = query.Where(i =>
                (i.FAuxType != null && globalTypeNames.Contains(i.FAuxType)) ||
                (i.FAccountSetId == request.AccountSetId &&
                 (currentOrgId == 0 || i.FOrgId == currentOrgId || i.FOrgId == 0)));
        }

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            query = query.Where(i =>
                i.FCode.Contains(request.Keyword) ||
                i.FName.Contains(request.Keyword) ||
                (i.FShortName != null && i.FShortName.Contains(request.Keyword)));
        }

        var items = await query.OrderBy(i => i.FCode).ToListAsync();
        return items.Select(MapItemToDto).ToList();
    }

    public async Task<AuxiliaryItemDto> CreateItemByAccountSetAsync(AuxiliaryItemCreateRequest request)
    {
        var currentOrgId = GetCurrentOrgId();

        // 查找或创建对应的辅助核算类型（用 AuxType 作为类型名称）
        var auxType = await _typeRepository.Query()
            .FirstOrDefaultAsync(t => t.FName == request.AuxType);

        if (auxType == null)
        {
            // 类别不存在，自动新建类别
            auxType = new FinAuxiliaryType
            {
                FName = request.AuxType,
                FStatus = 1,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            await _typeRepository.AddAsync(auxType);
        }

        // 根据类型表中的 FScope 判断是否为全局类型（不分组织、不分账套）
        var isGlobal = auxType?.FScope == "global";

        // 根据 auxType 决定编码来源
        string code;
        if (request.AuxType == "project")
        {
            code = await _codeRuleService.GenerateNextCodeAsync("FIN_AUX_PJ");
        }
        else if (request.AuxType == "business_unit")
        {
            code = await _codeRuleService.GenerateNextCodeAsync("FIN_AUX_BU");
        }
        else if (request.AuxType == "cash_flow")
        {
            code = await _codeRuleService.GenerateNextCodeAsync("FIN_AUX");
        }
        else
        {
            // 自定义类别：优先使用用户输入的编码
            code = request.Code ?? string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                // 如果自定义类别也没输入编码，回退到默认规则
                code = await _codeRuleService.GenerateNextCodeAsync("FIN_AUX");
            }
        }

        var item = new FinAuxiliaryItem
        {
            FAccountSetId = isGlobal ? 0 : request.AccountSetId,  // 全局数据账套ID为0
            FOrgId = isGlobal ? 0 : currentOrgId,                 // 全局数据组织ID为0
            FAuxType = request.AuxType,
            FCode = code,
            FName = request.Name,
            FShortName = request.ShortName,
            FContact = request.Contact,
            FPhone = request.Phone,
            FAddress = request.Address,
            FRemark = request.Remark,
            FEnableStatus = 1,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _itemRepository.AddAsync(item);
        return MapItemToDto(item);
    }

    public async Task<AuxiliaryItemDto?> UpdateItemByAccountSetAsync(long id, AuxiliaryItemCreateRequest request)
    {
        var item = await _itemRepository.GetByIdAsync(id);
        if (item == null) return null;

        // 外部来源项目：编码和名称均不可手动修改，仅允许修改备注等辅助字段
        if (item.FSourceType != null)
        {
            item.FRemark = request.Remark;
            // 不更新 FName 和 FCode
        }
        else
        {
            // 手动创建项目
            var isUsed = await CheckItemUsageAsync(id);
            if (isUsed)
            {
                // 被凭证引用：只允许修改名称、简称、备注
                item.FName = request.Name;
                item.FShortName = request.ShortName;
                item.FRemark = request.Remark;
                // 不更新 FCode
            }
            else
            {
                // 未被引用：全部可改
                item.FName = request.Name;
                item.FCode = request.Code ?? item.FCode;
                item.FShortName = request.ShortName;
                item.FContact = request.Contact;
                item.FPhone = request.Phone;
                item.FAddress = request.Address;
                item.FRemark = request.Remark;
            }
        }
        item.FUpdatedTime = DateTime.Now;

        await _itemRepository.UpdateAsync(item);
        return MapItemToDto(item);
    }

    public async Task<bool> DeleteItemByIdAsync(long id)
    {
        var item = await _itemRepository.GetByIdAsync(id);
        if (item == null) return false;

        // 删除保护：检查是否被凭证引用
        var isUsed = await CheckItemUsageAsync(id);
        if (isUsed)
        {
            throw new InvalidOperationException("该辅助核算项目已被凭证使用，无法删除");
        }

        await _itemRepository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> CheckItemUsageAsync(long id)
    {
        var item = await _itemRepository.GetByIdAsync(id);
        if (item == null) return false;

        // 查询凭证分录中是否有引用该辅助核算项目的 FAuxiliaryJson
        // FAuxiliaryJson 存储格式如 [{"type":"客户","code":"KH001","name":"..."},...]
        var auxType = item.FAuxType ?? string.Empty;
        var auxCode = item.FCode;

        // 检查 JSON 中是否包含该类型和编码
        var isUsed = await _voucherEntryRepository.Query()
            .AnyAsync(e => e.FAuxiliaryJson != null &&
                e.FAuxiliaryJson.Contains($"\"code\":\"{auxCode}\"") &&
                e.FAuxiliaryJson.Contains($"\"type\":\"{auxType}\""));

        return isUsed;
    }

    public async Task<bool> CheckCodeExistsAsync(long accountSetId, string code, long excludeId)
    {
        var currentOrgId = GetCurrentOrgId();
        return await _itemRepository.Query()
            .AnyAsync(i =>
                i.FAccountSetId == accountSetId &&
                i.FCode == code &&
                i.FID != excludeId &&
                i.FOrgId == currentOrgId);
    }

    public async Task<(bool isUnique, string? conflictField)> CheckUniqueAsync(
        long accountSetId, string auxType, string? code, string? name, long? excludeId = null)
    {
        var currentOrgId = GetCurrentOrgId();

        // 检查编码唯一性
        if (!string.IsNullOrEmpty(code))
        {
            var codeExists = await _itemRepository.Query()
                .AnyAsync(i => i.FAccountSetId == accountSetId
                    && i.FOrgId == currentOrgId
                    && i.FAuxType == auxType
                    && i.FCode == code
                    && (excludeId == null || i.FID != excludeId));
            if (codeExists) return (false, "code");
        }

        // 检查名称唯一性
        if (!string.IsNullOrEmpty(name))
        {
            var nameExists = await _itemRepository.Query()
                .AnyAsync(i => i.FAccountSetId == accountSetId
                    && i.FOrgId == currentOrgId
                    && i.FAuxType == auxType
                    && i.FName == name
                    && (excludeId == null || i.FID != excludeId));
            if (nameExists) return (false, "name");
        }

        return (true, null);
    }

    #endregion

    #region 账套组织范围辅助方法

    private async Task<(bool success, long orgId, string? errorMessage)> GetAccountSetOrgIdAsync(long accountSetId)
    {
        var accountSet = await _dbContext.Set<FinAccountSet>()
            .FirstOrDefaultAsync(a => a.FID == accountSetId);
        if (accountSet == null)
            return (false, 0, "账套不存在");
        if (accountSet.FOrgId == 0)
            return (false, 0, "请先关联账套和组织");
        return (true, accountSet.FOrgId, null);
    }

    // 跨模块查询用轻量DTO（避免循环引用）
    private class RawCustomerDto
    {
        public long Id { get; set; }
        public string? CustomerCode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Contact { get; set; }
        public string? Phone { get; set; }
    }

    private class RawSupplierDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ShortName { get; set; }
        public string? Contact { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }

    private class RawBrandDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private class RawNetworkPointDto
    {
        public string Code { get; set; } = string.Empty;
        public string? ShortName { get; set; }
        public string? Address { get; set; }
        public string? Manager { get; set; }
        public string? Phone { get; set; }
    }

    #endregion

    #region 组织架构集成

    public async Task<List<AvailableDepartmentDto>> GetAvailableDepartmentsAsync(long accountSetId)
    {
        var currentOrgId = GetCurrentOrgId();

        // 通过账套组织范围过滤部门
        var (success, accountSetOrgId, _) = await GetAccountSetOrgIdAsync(accountSetId);

        // 获取所有启用的组织节点（含 OrgType 导航属性用于类型判断）
        var allOrgs = await _dbContext.Set<SysOrganization>()
            .Include(o => o.OrgType)
            .Where(o => o.FStatus == 1)
            .ToListAsync();

        // 递归获取 accountSetOrgId 下所有后代节点的 ID
        var descendantIds = new HashSet<long>();
        if (success && accountSetOrgId > 0)
        {
            var lookup = allOrgs.ToLookup(o => o.FParentId);
            var queue = new Queue<long>();
            queue.Enqueue(accountSetOrgId);
            while (queue.Count > 0)
            {
                var parentId = queue.Dequeue();
                foreach (var child in lookup[parentId])
                {
                    if (child.FID != accountSetOrgId) // 排除组织本身
                        descendantIds.Add(child.FID);
                    queue.Enqueue(child.FID);
                }
            }
        }

        // 从所有后代中筛选出部门类型节点（OrgType.FCode == "DEPT"）
        var departments = allOrgs
            .Where(o => descendantIds.Contains(o.FID)
                && o.OrgType != null && o.OrgType.FCode == "DEPT")
            .ToList();

        // 查询已添加为辅助核算项目的部门ID
        var existingSourceIds = await _itemRepository.Query()
            .Where(i => i.FOrgId == currentOrgId && i.FAccountSetId == accountSetId
                && i.FSourceType == "SYS组织架构" && i.FSourceId != null)
            .Select(i => i.FSourceId!.Value)
            .ToListAsync();

        var existingSet = new HashSet<long>(existingSourceIds);

        return departments
            .Where(d => !existingSet.Contains(d.FID))
            .Select(d => new AvailableDepartmentDto
            {
                Id = d.FID,
                Name = d.FName,
                Code = d.FCode
            })
            .ToList();
    }

    public async Task<List<AvailableEmployeeDto>> GetAvailableEmployeesAsync(long accountSetId)
    {
        var currentOrgId = GetCurrentOrgId();

        // 通过账套组织范围过滤员工
        var (success, accountSetOrgId, _) = await GetAccountSetOrgIdAsync(accountSetId);

        // 获取账套关联组织及其所有下级组织的 ID 集合
        List<long> orgIds;
        if (success && accountSetOrgId > 0)
        {
            // 查询该组织及其所有下级组织（FParentId 指向父组织）
            orgIds = await _dbContext.Set<SysOrganization>()
                .Where(o => o.FParentId == accountSetOrgId || o.FID == accountSetOrgId)
                .Select(o => o.FID)
                .ToListAsync();
        }
        else
        {
            orgIds = new List<long> { currentOrgId };
        }

        var userOrgs = await _dbContext.Set<SysUserOrganization>()
            .Include(uo => uo.User)
            .Where(uo => orgIds.Contains(uo.FOrgId) && uo.FStatus == 1)
            .ToListAsync();

        // 查询已添加为辅助核算项目的用户ID
        var existingSourceIds = await _itemRepository.Query()
            .Where(i => i.FOrgId == currentOrgId && i.FAccountSetId == accountSetId
                && i.FSourceType == "SYS用户" && i.FSourceId != null)
            .Select(i => i.FSourceId!.Value)
            .ToListAsync();

        var existingSet = new HashSet<long>(existingSourceIds);

        return userOrgs
            .Where(uo => !existingSet.Contains(uo.FUserId))
            .Select(uo => new AvailableEmployeeDto
            {
                UserId = uo.FUserId,
                Name = uo.User.FName,
                Account = uo.User.FAccount,
                Phone = uo.User.FPhone
            })
            .ToList();
    }

    public async Task<List<AuxiliaryItemDto>> AddFromOrganizationAsync(AddFromOrgRequest request)
    {
        var currentOrgId = GetCurrentOrgId();
        var result = new List<AuxiliaryItemDto>();

        // 确保"department"辅助核算类型存在
        var auxType = await _typeRepository.Query()
            .FirstOrDefaultAsync(t => t.FName == "department");
        if (auxType == null)
        {
            auxType = new FinAuxiliaryType
            {
                FName = "department",
                FStatus = 1,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            await _typeRepository.AddAsync(auxType);
        }

        foreach (var orgId in request.OrgIds)
        {
            // 检查是否已存在
            var exists = await _itemRepository.Query()
                .AnyAsync(i => i.FOrgId == currentOrgId && i.FAccountSetId == request.AccountSetId
                    && i.FSourceType == "SYS组织架构" && i.FSourceId == orgId);
            if (exists) continue;

            var org = await _dbContext.Set<SysOrganization>().FindAsync(orgId);
            if (org == null) continue;

            var item = new FinAuxiliaryItem
            {
                FAccountSetId = request.AccountSetId,
                FOrgId = currentOrgId,
                FAuxType = "department",
                FCode = org.FCode,
                FName = org.FName,
                FEnableStatus = 1,
                FSourceType = "SYS组织架构",
                FSourceId = orgId,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };

            await _itemRepository.AddAsync(item);
            result.Add(MapItemToDto(item));
        }

        return result;
    }

    public async Task<List<AuxiliaryItemDto>> AddFromUserAsync(AddFromUserRequest request)
    {
        var currentOrgId = GetCurrentOrgId();
        var result = new List<AuxiliaryItemDto>();

        // 确保"employee"辅助核算类型存在
        var auxType = await _typeRepository.Query()
            .FirstOrDefaultAsync(t => t.FName == "employee");
        if (auxType == null)
        {
            auxType = new FinAuxiliaryType
            {
                FName = "employee",
                FStatus = 1,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            await _typeRepository.AddAsync(auxType);
        }

        foreach (var userId in request.UserIds)
        {
            // 检查是否已存在
            var exists = await _itemRepository.Query()
                .AnyAsync(i => i.FOrgId == currentOrgId && i.FAccountSetId == request.AccountSetId
                    && i.FSourceType == "SYS用户" && i.FSourceId == userId);
            if (exists) continue;

            var user = await _dbContext.Set<SysUser>().FindAsync(userId);
            if (user == null) continue;

            var item = new FinAuxiliaryItem
            {
                FAccountSetId = request.AccountSetId,
                FOrgId = currentOrgId,
                FAuxType = "employee",
                FCode = user.FAccount,
                FName = user.FName,
                FPhone = user.FPhone,
                FEnableStatus = 1,
                FSourceType = "SYS用户",
                FSourceId = userId,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };

            await _itemRepository.AddAsync(item);
            result.Add(MapItemToDto(item));
        }

        return result;
    }

    #endregion

    #region 客户/供应商/快递品牌集成

    public async Task<List<AvailableCustomerDto>> GetAvailableCustomersAsync(long accountSetId)
    {
        var currentOrgId = GetCurrentOrgId();
        var (success, accountSetOrgId, _) = await GetAccountSetOrgIdAsync(accountSetId);

        // 通过原生SQL查询CRM客户（避免循环引用）
        var sql = success && accountSetOrgId > 0
            ? "SELECT FID AS [Id], [F编号] AS [CustomerCode], [F简称] AS [Name], [F全称] AS [FullName], [F联系人] AS [Contact], [F电话] AS [Phone] FROM [CRM客户] WHERE [F编号] IS NOT NULL AND [F编号] != '' AND [F组织ID] = {0}"
            : "SELECT FID AS [Id], [F编号] AS [CustomerCode], [F简称] AS [Name], [F全称] AS [FullName], [F联系人] AS [Contact], [F电话] AS [Phone] FROM [CRM客户] WHERE [F编号] IS NOT NULL AND [F编号] != ''";

        var customers = success && accountSetOrgId > 0
            ? await _dbContext.Database.SqlQueryRaw<RawCustomerDto>(sql, accountSetOrgId).ToListAsync()
            : await _dbContext.Database.SqlQueryRaw<RawCustomerDto>(sql).ToListAsync();

        // 排除已添加的客户
        var existingSourceIds = await _itemRepository.Query()
            .Where(i => i.FOrgId == currentOrgId && i.FAccountSetId == accountSetId
                && i.FSourceType == "CRM客户" && i.FSourceId != null)
            .Select(i => i.FSourceId!.Value)
            .ToListAsync();

        var existingSet = new HashSet<long>(existingSourceIds);

        return customers
            .Where(c => !existingSet.Contains(c.Id))
            .Select(c => new AvailableCustomerDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode ?? "",
                Name = c.Name,
                Contact = c.Contact,
                Phone = c.Phone
            })
            .ToList();
    }

    public async Task<List<AvailableSupplierDto>> GetAvailableSuppliersAsync(long accountSetId)
    {
        var currentOrgId = GetCurrentOrgId();

        var suppliers = await _dbContext.Database.SqlQueryRaw<RawSupplierDto>(
            "SELECT FID AS [Id], [F编码] AS [Code], [F全称] AS [Name], [F简称] AS [ShortName], [F联系人] AS [Contact], [F电话] AS [Phone], [F地址] AS [Address] FROM [SUP供应商] WHERE [F状态] = 1")
            .ToListAsync();

        // 排除已添加的供应商
        var existingSourceIds = await _itemRepository.Query()
            .Where(i => i.FOrgId == currentOrgId && i.FAccountSetId == accountSetId
                && i.FSourceType == "SUP供应商" && i.FSourceId != null)
            .Select(i => i.FSourceId!.Value)
            .ToListAsync();

        var existingSet = new HashSet<long>(existingSourceIds);

        return suppliers
            .Where(s => !existingSet.Contains(s.Id))
            .Select(s => new AvailableSupplierDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                ShortName = s.ShortName,
                Contact = s.Contact
            })
            .ToList();
    }

    public async Task<List<AvailableBrandDto>> GetAvailableBrandsAsync(long accountSetId)
    {
        var currentOrgId = GetCurrentOrgId();

        var brands = await _dbContext.Database.SqlQueryRaw<RawBrandDto>(
            "SELECT FID AS [Id], [F编码] AS [Code], [F名称] AS [Name] FROM [EXP品牌] WHERE [F状态] = 1")
            .ToListAsync();

        // 排除已添加的品牌
        var existingSourceIds = await _itemRepository.Query()
            .Where(i => i.FOrgId == currentOrgId && i.FAccountSetId == accountSetId
                && i.FSourceType == "EXP品牌" && i.FSourceId != null)
            .Select(i => i.FSourceId!.Value)
            .ToListAsync();

        var existingSet = new HashSet<long>(existingSourceIds);

        return brands
            .Where(b => !existingSet.Contains(b.Id))
            .Select(b => new AvailableBrandDto
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name
            })
            .ToList();
    }

    public async Task<List<AuxiliaryItemDto>> AddFromCustomerAsync(AddFromCustomerRequest request)
    {
        var currentOrgId = GetCurrentOrgId();
        var result = new List<AuxiliaryItemDto>();

        // 确保"customer"辅助核算类型存在
        var auxType = await _typeRepository.Query()
            .FirstOrDefaultAsync(t => t.FName == "customer");
        if (auxType == null)
        {
            auxType = new FinAuxiliaryType
            {
                FName = "customer",
                FStatus = 1,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            await _typeRepository.AddAsync(auxType);
        }

        foreach (var customerId in request.CustomerIds)
        {
            // 检查是否已存在
            var exists = await _itemRepository.Query()
                .AnyAsync(i => i.FOrgId == currentOrgId && i.FAccountSetId == request.AccountSetId
                    && i.FSourceType == "CRM客户" && i.FSourceId == customerId);
            if (exists) continue;

            var customer = await _dbContext.Database.SqlQueryRaw<RawCustomerDto>(
                "SELECT FID AS [Id], [F编号] AS [CustomerCode], [F简称] AS [Name], [F全称] AS [FullName], [F联系人] AS [Contact], [F电话] AS [Phone] FROM [CRM客户] WHERE FID = {0}",
                customerId).FirstOrDefaultAsync();
            if (customer == null || string.IsNullOrEmpty(customer.CustomerCode)) continue;

            var item = new FinAuxiliaryItem
            {
                FAccountSetId = request.AccountSetId,
                FOrgId = currentOrgId,
                FAuxType = "customer",
                FCode = customer.CustomerCode,
                FName = customer.Name,
                FContact = customer.Contact,
                FPhone = customer.Phone,
                FEnableStatus = 1,
                FSourceType = "CRM客户",
                FSourceId = customerId,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };

            await _itemRepository.AddAsync(item);
            result.Add(MapItemToDto(item));
        }

        return result;
    }

    public async Task<List<AuxiliaryItemDto>> AddFromSupplierAsync(AddFromSupplierRequest request)
    {
        var currentOrgId = GetCurrentOrgId();
        var result = new List<AuxiliaryItemDto>();

        // 确保"supplier"辅助核算类型存在
        var auxType = await _typeRepository.Query()
            .FirstOrDefaultAsync(t => t.FName == "supplier");
        if (auxType == null)
        {
            auxType = new FinAuxiliaryType
            {
                FName = "supplier",
                FStatus = 1,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            await _typeRepository.AddAsync(auxType);
        }

        foreach (var supplierId in request.SupplierIds)
        {
            // 检查是否已存在
            var exists = await _itemRepository.Query()
                .AnyAsync(i => i.FOrgId == currentOrgId && i.FAccountSetId == request.AccountSetId
                    && i.FSourceType == "SUP供应商" && i.FSourceId == supplierId);
            if (exists) continue;

            var supplier = await _dbContext.Database.SqlQueryRaw<RawSupplierDto>(
                "SELECT FID AS [Id], [F编码] AS [Code], [F全称] AS [Name], [F简称] AS [ShortName], [F联系人] AS [Contact], [F电话] AS [Phone], [F地址] AS [Address] FROM [SUP供应商] WHERE FID = {0}",
                supplierId).FirstOrDefaultAsync();
            if (supplier == null) continue;

            var item = new FinAuxiliaryItem
            {
                FAccountSetId = request.AccountSetId,
                FOrgId = currentOrgId,
                FAuxType = "supplier",
                FCode = supplier.Code,
                FName = supplier.ShortName ?? supplier.Name,
                FShortName = supplier.ShortName,
                FContact = supplier.Contact,
                FPhone = supplier.Phone,
                FAddress = supplier.Address,
                FEnableStatus = 1,
                FSourceType = "SUP供应商",
                FSourceId = supplierId,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };

            await _itemRepository.AddAsync(item);
            result.Add(MapItemToDto(item));
        }

        return result;
    }

    public async Task<List<AuxiliaryItemDto>> AddFromBrandAsync(AddFromBrandRequest request)
    {
        var currentOrgId = GetCurrentOrgId();
        var result = new List<AuxiliaryItemDto>();

        // 确保"express_brand"辅助核算类型存在
        var auxType = await _typeRepository.Query()
            .FirstOrDefaultAsync(t => t.FName == "express_brand");
        if (auxType == null)
        {
            auxType = new FinAuxiliaryType
            {
                FName = "express_brand",
                FStatus = 1,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            await _typeRepository.AddAsync(auxType);
        }

        var isGlobal = auxType?.FScope == "global";

        foreach (var brandId in request.BrandIds)
        {
            // 检查是否已存在（全局品牌只要 SourceId 相同就算重复，不用区分 org/accountSet）
            // 使用 IgnoreQueryFilters 绕过 IOrgScoped 全局过滤器，避免非根组织下查不到 FOrgId=0 的全局品牌导致重复导入
            var exists = await _dbContext.Set<FinAuxiliaryItem>().IgnoreQueryFilters()
                .AnyAsync(i => i.FSourceType == "EXP品牌" && i.FSourceId == brandId);
            if (exists) continue;

            var brand = await _dbContext.Database.SqlQueryRaw<RawBrandDto>(
                "SELECT FID AS [Id], [F编码] AS [Code], [F名称] AS [Name] FROM [EXP品牌] WHERE FID = {0}",
                brandId).FirstOrDefaultAsync();
            if (brand == null) continue;

            var item = new FinAuxiliaryItem
            {
                FAccountSetId = isGlobal ? 0 : request.AccountSetId,
                FOrgId = isGlobal ? 0 : currentOrgId,
                FAuxType = "express_brand",
                FCode = brand.Code,
                FName = brand.Name,
                FEnableStatus = 1,
                FSourceType = "EXP品牌",
                FSourceId = brandId,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };

            await _itemRepository.AddAsync(item);
            result.Add(MapItemToDto(item));
        }

        return result;
    }

    public async Task<List<AvailableNetworkPointDto>> GetAvailableNetworkPointsAsync(long accountSetId)
    {
        var currentOrgId = GetCurrentOrgId();

        var networkPoints = await _dbContext.Database.SqlQueryRaw<RawNetworkPointDto>(
            "SELECT [F编号] AS [Code], [F网点简称] AS [ShortName], [F地址] AS [Address], [F负责人] AS [Manager], [F联系电话] AS [Phone] FROM [EXP快递网点] WHERE [F状态] = 1 AND [F组织ID] = {0}",
            currentOrgId).ToListAsync();

        // 排除已添加的网点（通过 FSourceType + FCode 匹配）
        var existingCodes = await _itemRepository.Query()
            .Where(i => i.FOrgId == currentOrgId && i.FAccountSetId == accountSetId
                && i.FSourceType == "EXP快递网点")
            .Select(i => i.FCode)
            .ToListAsync();

        var existingSet = new HashSet<string>(existingCodes);

        return networkPoints
            .Where(np => !existingSet.Contains(np.Code))
            .Select(np => new AvailableNetworkPointDto
            {
                Code = np.Code,
                Name = np.ShortName,
                Address = np.Address,
                Manager = np.Manager,
                Phone = np.Phone
            })
            .ToList();
    }

    public async Task<List<AuxiliaryItemDto>> AddFromNetworkPointAsync(AddFromNetworkPointRequest request)
    {
        var currentOrgId = GetCurrentOrgId();
        var result = new List<AuxiliaryItemDto>();

        // 确保"outlet"辅助核算类型存在
        var auxType = await _typeRepository.Query()
            .FirstOrDefaultAsync(t => t.FName == "outlet");
        if (auxType == null)
        {
            auxType = new FinAuxiliaryType
            {
                FName = "outlet",
                FStatus = 1,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            await _typeRepository.AddAsync(auxType);
        }

        foreach (var code in request.NetworkPointCodes)
        {
            // 检查是否已存在（通过 FSourceType + FCode 匹配）
            var exists = await _itemRepository.Query()
                .AnyAsync(i => i.FOrgId == currentOrgId && i.FAccountSetId == request.AccountSetId
                    && i.FSourceType == "EXP快递网点" && i.FCode == code);
            if (exists) continue;

            var networkPoint = await _dbContext.Database.SqlQueryRaw<RawNetworkPointDto>(
                "SELECT [F编号] AS [Code], [F网点简称] AS [ShortName], [F地址] AS [Address], [F负责人] AS [Manager], [F联系电话] AS [Phone] FROM [EXP快递网点] WHERE [F编号] = {0}",
                code).FirstOrDefaultAsync();
            if (networkPoint == null) continue;

            var item = new FinAuxiliaryItem
            {
                FAccountSetId = request.AccountSetId,
                FOrgId = currentOrgId,
                FAuxType = "outlet",
                FCode = networkPoint.Code,
                FName = networkPoint.ShortName ?? networkPoint.Code,
                FAddress = networkPoint.Address,
                FContact = networkPoint.Manager,
                FPhone = networkPoint.Phone,
                FEnableStatus = 1,
                FSourceType = "EXP快递网点",
                FSourceId = null,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };

            await _itemRepository.AddAsync(item);
            result.Add(MapItemToDto(item));
        }

        return result;
    }

    #endregion
}
