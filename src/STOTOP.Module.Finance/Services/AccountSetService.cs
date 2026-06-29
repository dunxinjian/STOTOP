using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class AccountSetService
{
    private readonly IRepository<FinAccountSet> _accountSetRepository;
    private readonly IRepository<FinAccount> _accountRepository;
    private readonly IRepository<FinAccountPeriod> _periodRepository;
    private readonly IRepository<FinVoucher> _voucherRepository;
    private readonly IRepository<FinAccountBalance> _balanceRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAccountTemplateService _accountTemplateService;
    private readonly IAccountSetAuthorizationService _accountSetAuthService;
    private readonly STOTOPDbContext _context;
    private readonly ILogger<AccountSetService> _logger;

    public AccountSetService(
        IRepository<FinAccountSet> accountSetRepository,
        IRepository<FinAccount> accountRepository,
        IRepository<FinAccountPeriod> periodRepository,
        IRepository<FinVoucher> voucherRepository,
        IRepository<FinAccountBalance> balanceRepository,
        IHttpContextAccessor httpContextAccessor,
        IAccountTemplateService accountTemplateService,
        IAccountSetAuthorizationService accountSetAuthService,
        STOTOPDbContext context,
        ILogger<AccountSetService> logger)
    {
        _accountSetRepository = accountSetRepository;
        _accountRepository = accountRepository;
        _periodRepository = periodRepository;
        _voucherRepository = voucherRepository;
        _balanceRepository = balanceRepository;
        _httpContextAccessor = httpContextAccessor;
        _accountTemplateService = accountTemplateService;
        _accountSetAuthService = accountSetAuthService;
        _context = context;
        _logger = logger;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    private long GetCurrentUserId()
    {
        var userIdStr = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdStr) && long.TryParse(userIdStr, out var userId))
            return userId;
        return 0;
    }

    private bool IsAdmin()
    {
        var userName = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
        return string.Equals(userName, "admin", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取所有启用的账套列表
    /// </summary>
    public async Task<List<AccountSetDto>> GetAllAsync(long? requestOrgId = null)
    {
        var orgId = requestOrgId ?? GetCurrentOrgId();
        var userId = GetCurrentUserId();
        var isAdmin = IsAdmin();

        _logger.LogDebug("GetAllAsync: userId={UserId}, orgId={OrgId}, isAdmin={IsAdmin}", userId, orgId, isAdmin);

        var query = _accountSetRepository.Query()
            .Where(a => a.FStatus == 1);

        // 组织过滤：显式传入 orgId 时始终过滤（含 admin），确保账套选择器按组织隔离
        if (orgId > 0)
        {
            query = query.Where(a => a.FOrgId == orgId);
        }

        // 授权过滤 - admin 跳过
        if (!isAdmin)
        {
            if (userId <= 0)
            {
                // 无法识别用户身份，返回空列表
                _logger.LogWarning("GetAllAsync: 无法获取当前用户ID，返回空账套列表");
                return new List<AccountSetDto>();
            }

            var authorizedIds = await _accountSetAuthService.GetUserAccountSetIdsAsync(userId, orgId);
            _logger.LogDebug("GetAllAsync: 用户 {UserId} 被授权的账套IDs: [{Ids}]", userId, string.Join(",", authorizedIds));
            query = query.Where(a => authorizedIds.Contains(a.FID));
        }

        var list = await query
            .OrderBy(a => a.FSortOrder)
            .ThenBy(a => a.FID)
            .ToListAsync();

        return list.Select(MapToDto).ToList();
    }

    /// <summary>
    /// 获取单个账套
    /// </summary>
    public async Task<AccountSetDto?> GetByIdAsync(long id)
    {
        var entity = await _accountSetRepository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    /// <summary>
    /// 获取默认账套
    /// </summary>
    public async Task<AccountSetDto?> GetDefaultAsync()
    {
        var orgId = GetCurrentOrgId();
        var isAdmin = IsAdmin();
        var query = _accountSetRepository.Query()
            .Where(a => a.FIsDefault && a.FStatus == 1);

        if (!isAdmin && orgId > 0)
        {
            query = query.Where(a => a.FOrgId == orgId);
        }

        var entity = await query.FirstOrDefaultAsync();
        return entity == null ? null : MapToDto(entity);
    }

    /// <summary>
    /// 创建账套
    /// </summary>
    public async Task<AccountSetDto> CreateAsync(AccountSetDto dto)
    {
        var entity = new FinAccountSet
        {
            FName = dto.FName,
            FCode = dto.FCode,
            FCompanyName = dto.FCompanyName,
            FDescription = dto.FDescription,
            FIsDefault = dto.FIsDefault,
            FStatus = dto.FStatus == 0 ? 1 : dto.FStatus,
            FSortOrder = dto.FSortOrder,
            FStartYear = dto.FStartYear,
            FStartMonth = dto.FStartMonth,
            FOrgId = GetCurrentOrgId(),
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        // 如果设置为默认，取消其他默认
        if (entity.FIsDefault)
        {
            await ClearDefaultAsync();
        }

        await _accountSetRepository.AddAsync(entity);
        return MapToDto(entity);
    }

    /// <summary>
    /// 创建账套（支持模板ID）
    /// </summary>
    public async Task<AccountSetDto> CreateAsync(CreateAccountSetRequest request)
    {
        var entity = new FinAccountSet
        {
            FName = request.FName,
            FCode = request.FCode,
            FCompanyName = request.FCompanyName,
            FDescription = request.FDescription,
            FIsDefault = request.FIsDefault,
            FStatus = request.FStatus == 0 ? 1 : request.FStatus,
            FSortOrder = request.FSortOrder,
            FStartYear = request.FStartYear,
            FStartMonth = request.FStartMonth,
            FOrgId = GetCurrentOrgId(),
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        if (entity.FIsDefault)
        {
            await ClearDefaultAsync();
        }

        await _accountSetRepository.AddAsync(entity);

        // 如果指定了科目模板ID，应用模板科目到新账套
        if (request.TemplateId.HasValue && request.TemplateId.Value > 0)
        {
            await _accountTemplateService.ApplyTemplateAsync(request.TemplateId.Value, entity.FID);
        }

        // 自动为创建者授予财务经理角色（角色ID=1, 编码=fin_manager）
        var userId = GetCurrentUserId();
        if (userId > 0)
        {
            await _accountSetAuthService.GrantAsync(userId, entity.FID, 1, entity.FOrgId, userId);
        }

        return MapToDto(entity);
    }

    /// <summary>
    /// 修改账套
    /// </summary>
    public async Task<AccountSetDto?> UpdateAsync(long id, AccountSetDto dto)
    {
        var entity = await _accountSetRepository.GetByIdAsync(id);
        if (entity == null) return null;

        entity.FName = dto.FName;
        entity.FCode = dto.FCode;
        entity.FCompanyName = dto.FCompanyName;
        entity.FDescription = dto.FDescription;
        entity.FIsDefault = dto.FIsDefault;
        entity.FStatus = dto.FStatus;
        entity.FSortOrder = dto.FSortOrder;
        entity.FStartYear = dto.FStartYear;
        entity.FStartMonth = dto.FStartMonth;
        if (dto.FOrgId.HasValue && dto.FOrgId.Value > 0)
            entity.FOrgId = dto.FOrgId.Value;
        entity.FUpdatedTime = DateTime.Now;

        // 如果设置为默认，取消其他默认
        if (entity.FIsDefault)
        {
            await ClearDefaultAsync(entity.FID);
        }

        await _accountSetRepository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    /// <summary>
    /// 删除账套（仅允许删除无凭证数据的账套）
    /// </summary>
    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _accountSetRepository.GetByIdAsync(id);
        if (entity == null) return false;

        if (entity.FIsDefault)
        {
            throw new InvalidOperationException("默认账套不能删除");
        }

        // 检查是否有关联凭证
        var hasVouchers = await _voucherRepository.Query()
            .AnyAsync(v => v.FAccountSetId == id);

        if (hasVouchers)
        {
            throw new InvalidOperationException("该账套下已有凭证数据，无法删除");
        }

        await _accountSetRepository.DeleteAsync(id);
        return true;
    }

    /// <summary>
    /// 初始化账套：支持三种模板模式（空账套/行业标准/现有账套）
    /// </summary>
    /// <param name="id">账套ID</param>
    /// <param name="force">是否强制重新初始化</param>
    /// <param name="templateType">模板类型: empty | industry | existing，为null时走原逻辑（默认科目表）</param>
    /// <param name="industryCode">行业编码: standard(小企业标准) | express-delivery(快递行业)</param>
    /// <param name="sourceAccountSetId">源账套ID（existing模式必填）</param>
    public async Task<(bool Success, string Message, int AccountCount, int PeriodCount)> InitializeAccountSetAsync(
        long id, bool force = false,
        string? templateType = null,
        string? industryCode = null,
        long? sourceAccountSetId = null)
    {
        var accountSet = await _accountSetRepository.GetByIdAsync(id);
        if (accountSet == null)
            throw new InvalidOperationException("账套不存在");
    
        // 1. 检查是否已初始化（该账套下是否已有科目）
        var existingAccountCount = await _accountRepository.Query()
            .CountAsync(a => a.FAccountSetId == id);
    
        if (existingAccountCount > 0 && !force)
        {
            return (false, $"该账套已初始化（已有{existingAccountCount}个科目），如需重新初始化请选择强制模式", existingAccountCount, 0);
        }
    
        // 如果强制模式且已有科目，先清除旧数据
        if (existingAccountCount > 0 && force)
        {
            // 检查是否有凭证数据，有凭证则不允许重新初始化
            var hasVouchers = await _voucherRepository.Query()
                .AnyAsync(v => v.FAccountSetId == id);
            if (hasVouchers)
                throw new InvalidOperationException("该账套下已有凭证数据，无法重新初始化");
    
            // 清除旧的科目余额
            var oldBalances = await _balanceRepository.Query()
                .Where(b => b.FAccountSetId == id)
                .ToListAsync();
            foreach (var b in oldBalances)
                await _balanceRepository.DeleteAsync(b.FID);
    
            // 清除旧科目
            var oldAccounts = await _accountRepository.Query()
                .Where(a => a.FAccountSetId == id)
                .ToListAsync();
            foreach (var a in oldAccounts)
                await _accountRepository.DeleteAsync(a.FID);
    
            // 清除旧会计期间
            var oldPeriods = await _periodRepository.Query()
                .Where(p => p.FAccountSetId == id)
                .ToListAsync();
            foreach (var p in oldPeriods)
                await _periodRepository.DeleteAsync(p.FID);
        }
    
        // 2. 根据模板类型确定数据源
        long sourceId = 0;
        bool copyAccounts = true;
    
        if (!string.IsNullOrEmpty(templateType))
        {
            switch (templateType.ToLower())
            {
                case "empty":
                    // 空账套模式：不复制科目
                    copyAccounts = false;
                    break;
    
                case "industry":
                    // 行业模板模式
                    if (string.IsNullOrEmpty(industryCode))
                        throw new InvalidOperationException("行业模板模式需要指定 industryCode");
    
                    sourceId = industryCode.ToLower() switch
                    {
                        "standard" => 0,           // 小企业会计准则（默认科目表）
                        "express-delivery" => 2,  // 快递行业模板：取账套2(太仓美申)科目，与 FID3 模板数据源口径一致
                        _ => throw new InvalidOperationException($"不支持的行业模板编码: {industryCode}")
                    };
                    break;
    
                case "existing":
                    // 现有账套复制模式
                    if (!sourceAccountSetId.HasValue || sourceAccountSetId.Value <= 0)
                        throw new InvalidOperationException("现有账套复制模式需要指定 sourceAccountSetId");
    
                    // 验证源账套存在
                    var sourceSet = await _accountSetRepository.GetByIdAsync(sourceAccountSetId.Value);
                    if (sourceSet == null)
                        throw new InvalidOperationException($"源账套不存在: {sourceAccountSetId}");
    
                    sourceId = sourceAccountSetId.Value;
                    break;
    
                default:
                    throw new InvalidOperationException($"不支持的模板类型: {templateType}");
            }
        }
        else
        {
            // 向后兼容：templateType 为 null 时走原逻辑
            sourceId = 0;
            copyAccounts = true;
        }
    
        int accountCount = 0;
    
        // 3. 复制科目表（如果需要）
        if (copyAccounts)
        {
            var sourceAccounts = await GetSourceAccountsAsync(sourceId, id);
    
            if (!sourceAccounts.Any())
            {
                if (!string.IsNullOrEmpty(templateType) && templateType.ToLower() != "empty")
                    throw new InvalidOperationException($"模板数据源中未找到科目数据（源ID: {sourceId}）");
            }
            else
            {
                // 复制科目，维护父子关系的ID映射
                var idMapping = new Dictionary<long, long>();
    
                foreach (var srcAccount in sourceAccounts)
                {
                    var newParentId = srcAccount.FParentId;
                    if (srcAccount.FParentId > 0 && idMapping.ContainsKey(srcAccount.FParentId))
                    {
                        newParentId = idMapping[srcAccount.FParentId];
                    }
                    else if (srcAccount.FParentId > 0)
                    {
                        // 父级未找到映射，设为0（顶级）
                        newParentId = 0;
                    }
    
                    var newAccount = new FinAccount
                    {
                        FCode = srcAccount.FCode,
                        FName = srcAccount.FName,
                        FCategory = srcAccount.FCategory,
                        FBalanceDirection = srcAccount.FBalanceDirection,
                        FLevel = srcAccount.FLevel,
                        FParentId = newParentId,
                        FIsLeaf = srcAccount.FIsLeaf,
                        FAuxiliary = srcAccount.FAuxiliary,
                        FCurrency = srcAccount.FCurrency,
                        FUnit = srcAccount.FUnit,
                        FEnableStatus = srcAccount.FEnableStatus,
                        FAccountSetId = id,
                        FCreatedTime = DateTime.Now,
                        FUpdatedTime = DateTime.Now
                    };
    
                    await _accountRepository.AddAsync(newAccount);
                    idMapping[srcAccount.FID] = newAccount.FID;
                    accountCount++;
                }
            }
        }
    
        // 4. 创建会计期间（从起始年月到当前年月）
        int periodCount = 0;
        int startYear = accountSet.FStartYear > 0 ? accountSet.FStartYear : DateTime.Now.Year;
        int startMonth = accountSet.FStartMonth > 0 ? accountSet.FStartMonth : 1;
        int endYear = DateTime.Now.Year;
        int endMonth = DateTime.Now.Month;
        
        // 批量查询该账套已有的期间，用于幂等性检查
        var existingPeriods = await _periodRepository.Query()
            .Where(p => p.FAccountSetId == id)
            .Select(p => new { p.FYear, p.FPeriodNo })
            .ToListAsync();
        var existingPeriodSet = new HashSet<(int Year, int PeriodNo)>(
            existingPeriods.Select(p => (p.FYear, p.FPeriodNo)));
        
        // 从 startYear/startMonth 遍历到 endYear/endMonth
        int currentYear = startYear;
        int currentMonth = startMonth;
        while (new DateTime(currentYear, currentMonth, 1) <= new DateTime(endYear, endMonth, 1))
        {
            // 检查该期间是否已存在（幂等性）
            if (!existingPeriodSet.Contains((currentYear, currentMonth)))
            {
                var startDate = new DateTime(currentYear, currentMonth, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);
        
                var period = new FinAccountPeriod
                {
                    FYear = currentYear,
                    FPeriodNo = currentMonth,
                    FStartDate = startDate,
                    FEndDate = endDate,
                    FIsClosed = 0,
                    FStatus = 1,
                    FAccountSetId = id,
                    FOrgId = accountSet.FOrgId,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };
        
                await _periodRepository.AddAsync(period);
                periodCount++;
            }
        
            // 移动到下一个月
            currentMonth++;
            if (currentMonth > 12)
            {
                currentMonth = 1;
                currentYear++;
            }
        }

        // 确保所有期间数据持久化
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "保存会计期间时发生DbUpdateException，可能是并发初始化导致的唯一约束冲突，账套ID: {AccountSetId}", id);
        }
    
        // 5. 初始化科目余额（为叶子科目创建第一个期间的初始余额记录）
        var firstPeriod = await _periodRepository.Query()
            .Where(p => p.FAccountSetId == id)
            .OrderBy(p => p.FYear)
            .ThenBy(p => p.FPeriodNo)
            .FirstOrDefaultAsync();
    
        if (firstPeriod != null && accountCount > 0)
        {
            var leafAccounts = await _accountRepository.Query()
                .Where(a => a.FAccountSetId == id && a.FIsLeaf == 1)
                .ToListAsync();
    
            foreach (var leaf in leafAccounts)
            {
                var existingBalance = await _balanceRepository.Query()
                    .AnyAsync(b => b.FAccountId == leaf.FID && b.FPeriodId == firstPeriod.FID);
    
                if (!existingBalance)
                {
                    var balance = new FinAccountBalance
                    {
                        FPeriodId = firstPeriod.FID,
                        FAccountId = leaf.FID,
                        FBeginDebit = 0,
                        FBeginCredit = 0,
                        FCurrentDebit = 0,
                        FCurrentCredit = 0,
                        FEndDebit = 0,
                        FEndCredit = 0,
                        FAccountSetId = id,
                        FCreatedTime = DateTime.Now,
                        FUpdatedTime = DateTime.Now
                    };
                    await _balanceRepository.AddAsync(balance);
                }
            }
        }
    
        // 返回结果消息
        string message = templateType?.ToLower() switch
        {
            "empty" => "空账套初始化成功",
            "industry" => $"初始化成功：从行业模板[{industryCode}]复制了{accountCount}个科目，创建了{periodCount}个会计期间",
            "existing" => $"初始化成功：从账套[{sourceAccountSetId}]复制了{accountCount}个科目，创建了{periodCount}个会计期间",
            _ => $"初始化成功：复制了{accountCount}个科目，创建了{periodCount}个会计期间"
        };
    
        return (true, message, accountCount, periodCount);
    }
    
    /// <summary>
    /// 获取模板科目数量
    /// </summary>
    public async Task<int> GetTemplateAccountCountAsync(long sourceId)
    {
        return await _accountRepository.Query()
            .CountAsync(a => a.FAccountSetId == sourceId);
    }
    
    /// <summary>
    /// 获取模板科目列表
    /// </summary>
    public async Task<List<FinAccount>> GetTemplateAccountsAsync(long sourceId)
    {
        return await _accountRepository.Query()
            .Where(a => a.FAccountSetId == sourceId)
            .OrderBy(a => a.FLevel)
            .ThenBy(a => a.FCode)
            .ToListAsync();
    }
    
    /// <summary>
    /// 获取源科目数据
    /// </summary>
    private async Task<List<FinAccount>> GetSourceAccountsAsync(long sourceId, long targetId)
    {
        // 首先尝试从指定源ID获取
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == sourceId)
            .OrderBy(a => a.FLevel)
            .ThenBy(a => a.FCode)
            .ToListAsync();
    
        // 如果源ID为0且没有数据，尝试从默认账套获取
        if (!accounts.Any() && sourceId == 0)
        {
            var defaultSet = await _accountSetRepository.Query()
                .FirstOrDefaultAsync(a => a.FIsDefault && a.FStatus == 1 && a.FID != targetId);
    
            if (defaultSet != null)
            {
                accounts = await _accountRepository.Query()
                    .Where(a => a.FAccountSetId == defaultSet.FID)
                    .OrderBy(a => a.FLevel)
                    .ThenBy(a => a.FCode)
                    .ToListAsync();
            }
        }
    
        return accounts;
    }

    /// <summary>
    /// 清除其他账套的默认标志
    /// </summary>
    private async Task ClearDefaultAsync(long? excludeId = null)
    {
        var defaults = await _accountSetRepository.Query()
            .Where(a => a.FIsDefault)
            .ToListAsync();

        foreach (var item in defaults)
        {
            if (excludeId.HasValue && item.FID == excludeId.Value) continue;
            item.FIsDefault = false;
            item.FUpdatedTime = DateTime.Now;
            await _accountSetRepository.UpdateAsync(item);
        }
    }

    private static AccountSetDto MapToDto(FinAccountSet entity)
    {
        return new AccountSetDto
        {
            Id = entity.FID,
            FName = entity.FName,
            FCode = entity.FCode,
            FCompanyName = entity.FCompanyName,
            FDescription = entity.FDescription,
            FIsDefault = entity.FIsDefault,
            FStatus = entity.FStatus,
            FSortOrder = entity.FSortOrder,
            FStartYear = entity.FStartYear,
            FStartMonth = entity.FStartMonth,
            FOrgId = entity.FOrgId
        };
    }
}
