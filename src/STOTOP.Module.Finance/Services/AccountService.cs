using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Constants;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class AccountService : IAccountService
{
    private readonly IRepository<FinAccount> _accountRepository;
    private readonly IRepository<FinAccountBalance> _balanceRepository;
    private readonly IRepository<FinVoucherEntry> _voucherEntryRepository;
    private readonly ChangeTrackingService _changeTrackingService;

    public AccountService(
        IRepository<FinAccount> accountRepository,
        IRepository<FinAccountBalance> balanceRepository,
        IRepository<FinVoucherEntry> voucherEntryRepository,
        ChangeTrackingService changeTrackingService)
    {
        _accountRepository = accountRepository;
        _balanceRepository = balanceRepository;
        _voucherEntryRepository = voucherEntryRepository;
        _changeTrackingService = changeTrackingService;
    }

    public async Task<List<AccountTreeDto>> GetTreeAsync(string? category = null, long accountSetId = 0)
    {
        // 账套ID无效时直接返回空列表，避免查询无意义数据
        if (accountSetId <= 0)
            return new List<AccountTreeDto>();

        var query = _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId);
        
        if (!string.IsNullOrEmpty(category))
        {
            var subCategories = GetSubCategories(category);
            if (subCategories.Length > 0)
            {
                query = query.Where(a => subCategories.Contains(a.FCategory));
            }
            else
            {
                query = query.Where(a => a.FCategory == category);
            }
        }
        
        var accounts = await query.OrderBy(a => a.FCode).ToListAsync();
        return BuildTree(accounts);
    }

    /// <summary>
    /// 合法的科目类别集合（小企业会计准则预置，不支持自定义）
    /// </summary>
    private static readonly HashSet<string> ValidCategories = new()
    {
        "流动资产", "非流动资产", "流动负债", "非流动负债", "所有者权益", "成本",
        "损益", "营业收入", "营业成本", "营业税金及附加", "期间费用",
        "其他收益", "其他损失", "所得税费用", "以前年度损益调整"
    };

    /// <summary>
    /// 大类到数据库子类别的映射
    /// </summary>
    private static string[] GetSubCategories(string mainCategory)
    {
        return mainCategory switch
        {
            "资产" => new[] { "流动资产", "非流动资产" },
            "负债" => new[] { "流动负债", "非流动负债" },
            "权益" => new[] { "所有者权益" },
            "成本" => new[] { "成本" },
            // 损益子类（含大类名"损益"兼容早期数据）统一收敛到 FinAccountCategory 单一真源
            "损益" => FinAccountCategory.ProfitLossCategories,
            _ => Array.Empty<string>()
        };
    }

    public async Task<AccountDto?> GetByIdAsync(long id, long accountSetId)
    {
        var account = await LoadAccountAsync(id);
        // 科目 id 全局唯一、LoadAccount 不按账套过滤；校验科目所属账套==请求账套，
        // 不符按"不存在"返回，避免跨账套读取单条科目详情(F2)。
        if (account == null || account.FAccountSetId != accountSetId) return null;
        return MapToDto(account);
    }

    /// <summary>
    /// 按主键加载科目（账套级共享，不受组织过滤）。
    /// </summary>
    private Task<FinAccount?> LoadAccountAsync(long id)
        => _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.FID == id);

    public async Task<AccountDto> CreateAsync(CreateAccountRequest request, long accountSetId = 0)
    {
        if (accountSetId <= 0)
        {
            throw new InvalidOperationException("缺少有效的账套ID，无法创建科目");
        }

        var code = (request.Code ?? string.Empty).Trim();
        if (!code.All(char.IsDigit))
        {
            throw new InvalidOperationException("科目编码只能包含数字");
        }

        if (string.IsNullOrWhiteSpace(request.Category) || !ValidCategories.Contains(request.Category))
        {
            throw new InvalidOperationException($"科目类别 {request.Category} 无效");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("科目名称不能为空");
        }

        if (request.BalanceDirection != "借" && request.BalanceDirection != "贷")
        {
            throw new InvalidOperationException("科目余额方向只能为「借」或「贷」");
        }

        var existing = await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.FCode == code && a.FAccountSetId == accountSetId);

        if (existing != null)
        {
            throw new InvalidOperationException($"科目编码 {code} 已存在");
        }

        int level = 1;
        FinAccount? parent = null;
        if (request.ParentId > 0)
        {
            parent = await LoadAccountAsync(request.ParentId);
            if (parent == null)
            {
                throw new InvalidOperationException("上级科目不存在");
            }
            if (parent.FAccountSetId != accountSetId)
            {
                throw new InvalidOperationException("上级科目不属于当前账套");
            }
            level = parent.FLevel + 1;
            if (level > 4)
            {
                throw new InvalidOperationException("科目最多支持四级");
            }
            if (!code.StartsWith(parent.FCode))
            {
                throw new InvalidOperationException($"子科目编码必须以上级编码 {parent.FCode} 开头");
            }
            if (code.Length != parent.FCode.Length + 2)
            {
                throw new InvalidOperationException($"子科目编码长度应为 {parent.FCode.Length + 2} 位");
            }
        }
        else
        {
            if (code.Length != 4)
            {
                throw new InvalidOperationException("一级科目编码必须为4位数字");
            }
            if (code[0] < '1' || code[0] > '5')
            {
                throw new InvalidOperationException("一级科目编码必须以1（资产）/2（负债）/3（权益）/4（成本）/5（损益）开头");
            }
        }

        var account = new FinAccount
        {
            FCode = code,
            FName = request.Name,
            FCategory = request.Category,
            FBalanceDirection = request.BalanceDirection,
            FLevel = level,
            FParentId = request.ParentId,
            FIsLeaf = 1,
            FAuxiliary = request.Auxiliary,
            FCurrency = request.Currency,
            FUnit = request.Unit,
            FEnableStatus = 1,
            FAccountSetId = accountSetId,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _accountRepository.AddAsync(account);

        // 子科目插入成功后再翻转父级叶子标记，避免插入失败时父级状态被提前破坏
        if (parent != null && parent.FIsLeaf == 1)
        {
            parent.FIsLeaf = 0;
            await _accountRepository.UpdateAsync(parent);
        }

        return MapToDto(account);
    }

    public async Task<AccountDto?> UpdateAsync(long id, UpdateAccountRequest request)
    {
        var account = await LoadAccountAsync(id);
        if (account == null) return null;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("科目名称不能为空");
        }

        // 记录变更前的快照，用于字段级变更追踪
        var oldSnapshot = new FinAccount
        {
            FCode = account.FCode,
            FName = account.FName,
            FCategory = account.FCategory,
            FBalanceDirection = account.FBalanceDirection,
            FLevel = account.FLevel,
            FParentId = account.FParentId,
            FIsLeaf = account.FIsLeaf,
            FAuxiliary = account.FAuxiliary,
            FCurrency = account.FCurrency,
            FUnit = account.FUnit,
            FEnableStatus = account.FEnableStatus,
            FAccountSetId = account.FAccountSetId
        };

        account.FName = request.Name;
        account.FAuxiliary = request.Auxiliary;
        account.FCurrency = request.Currency;
        account.FUnit = request.Unit;
        account.FUpdatedTime = DateTime.Now;

        await _accountRepository.UpdateAsync(account);

        // 记录科目字段级变更
        await _changeTrackingService.TrackChangesAsync("科目", account.FID, oldSnapshot, account, account.FAccountSetId);

        return MapToDto(account);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var account = await LoadAccountAsync(id);
        if (account == null) return false;

        // 检查是否有凭证引用。凭证按组织隔离，但科目账套级共享，
        // 必须跨组织检查，否则其他组织的凭证引用会被漏检导致误删
        var hasVoucher = await _voucherEntryRepository.Query()
            .IgnoreQueryFilters()
            .AnyAsync(e => e.FAccountId == id);

        if (hasVoucher)
        {
            throw new InvalidOperationException("该科目已被凭证引用，无法删除");
        }

        // 检查是否有子科目
        var hasChildren = await _accountRepository.Query()
            .AnyAsync(a => a.FParentId == id);
        
        if (hasChildren)
        {
            throw new InvalidOperationException("该科目有下级科目，无法删除");
        }

        // 清理该科目的余额记录（期初余额等），避免留下孤儿行
        var balanceRows = await _balanceRepository.Query()
            .Where(b => b.FAccountId == id)
            .ToListAsync();
        foreach (var row in balanceRows)
        {
            await _balanceRepository.DeleteAsync(row.FID);
        }

        await _accountRepository.DeleteAsync(id);

        // 更新父级的IsLeaf状态
        if (account.FParentId > 0)
        {
            var siblings = await _accountRepository.Query()
                .Where(a => a.FParentId == account.FParentId)
                .ToListAsync();
            
            if (!siblings.Any())
            {
                var parent = await LoadAccountAsync(account.FParentId);
                if (parent != null)
                {
                    parent.FIsLeaf = 1;
                    await _accountRepository.UpdateAsync(parent);
                }
            }
        }
        
        return true;
    }

    public async Task<bool> ToggleStatusAsync(long id)
    {
        var account = await LoadAccountAsync(id);
        if (account == null) return false;

        // 停用时校验（启用无害，不校验）：被凭证引用/有启用下级/有余额则拒绝
        if (account.FEnableStatus == 1)
        {
            // 凭证按组织隔离、科目账套级共享，须跨组织检查（同 DeleteAsync）
            var hasVoucher = await _voucherEntryRepository.Query()
                .IgnoreQueryFilters()
                .AnyAsync(e => e.FAccountId == id);
            if (hasVoucher)
                throw new InvalidOperationException("该科目已被凭证引用，无法停用");

            var hasEnabledChildren = await _accountRepository.Query()
                .AnyAsync(a => a.FParentId == id && a.FEnableStatus == 1);
            if (hasEnabledChildren)
                throw new InvalidOperationException("该科目有启用的下级科目，无法停用");

            // 有余额时停用会让该余额从期初/试算合计中凭空消失，拒绝
            var hasBalance = await _balanceRepository.Query()
                .AnyAsync(b => b.FAccountId == id
                    && (b.FBeginDebit != 0 || b.FBeginCredit != 0 || b.FEndDebit != 0 || b.FEndCredit != 0));
            if (hasBalance)
                throw new InvalidOperationException("该科目存在余额，无法停用");
        }

        account.FEnableStatus = account.FEnableStatus == 1 ? 0 : 1;
        account.FUpdatedTime = DateTime.Now;
        await _accountRepository.UpdateAsync(account);
        return true;
    }

    public async Task<List<InitialBalanceDto>> GetInitialBalancesAsync(long accountSetId)
    {
        // 账套ID无效时直接返回空列表
        if (accountSetId <= 0)
            return new List<InitialBalanceDto>();

        // 查询该账套所有科目（含非末级，用于显示层级结构）
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId && a.FEnableStatus == 1)
            .OrderBy(a => a.FCode)
            .ToListAsync();

        // 查询期间0（期初余额专用期间）的余额记录
        var balances = await _balanceRepository.Query()
            .Where(b => b.FAccountSetId == accountSetId && b.FPeriodId == 0)
            .ToListAsync();

        // 计算哪些科目有子科目（非末级）
        var childLookup = accounts.ToLookup(a => a.FParentId);
        var accountsWithChildren = accounts
            .Where(a => childLookup[a.FID].Any())
            .Select(a => a.FID)
            .ToHashSet();

        // 末级科目取余额记录；非末级科目自下而上汇总末级余额（余额表不存非末级行）
        var balanceMap = balances
            .GroupBy(b => b.FAccountId)
            .ToDictionary(g => g.Key, g => (Debit: g.Sum(b => b.FBeginDebit), Credit: g.Sum(b => b.FBeginCredit)));

        (decimal Debit, decimal Credit) SumSubtree(FinAccount node)
        {
            if (!accountsWithChildren.Contains(node.FID))
            {
                return balanceMap.TryGetValue(node.FID, out var v) ? v : (0m, 0m);
            }
            decimal debit = 0, credit = 0;
            foreach (var child in childLookup[node.FID])
            {
                var (d, c) = SumSubtree(child);
                debit += d;
                credit += c;
            }
            return (debit, credit);
        }

        return accounts.Select(a =>
        {
            var (debit, credit) = SumSubtree(a);
            return new InitialBalanceDto
            {
                AccountId = a.FID,
                AccountCode = a.FCode,
                AccountName = a.FName,
                BalanceDirection = a.FBalanceDirection,
                Level = a.FLevel,
                DebitBalance = debit,
                CreditBalance = credit,
                IsLeaf = !accountsWithChildren.Contains(a.FID)
            };
        }).ToList();
    }

    public async Task<bool> SaveInitialBalancesAsync(SaveInitialBalancesRequest request)
    {
        if (request.AccountSetId <= 0)
        {
            throw new InvalidOperationException("缺少有效的账套ID，无法保存期初余额");
        }

        // 校验科目归属与末级状态，不信任客户端提交的数据
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == request.AccountSetId)
            .Select(a => new { a.FID, a.FParentId, a.FCategory, a.FCode })
            .ToListAsync();
        var accountIds = accounts.Select(a => a.FID).ToHashSet();
        var parentIds = accounts.Where(a => a.FParentId > 0).Select(a => a.FParentId).ToHashSet();
        var accountInfoMap = accounts.ToDictionary(a => a.FID, a => (a.FCategory, a.FCode));

        foreach (var item in request.Items)
        {
            if (!accountIds.Contains(item.AccountId))
            {
                throw new InvalidOperationException($"科目 {item.AccountId} 不属于当前账套");
            }
            if (parentIds.Contains(item.AccountId))
            {
                throw new InvalidOperationException("仅末级科目可录入期初余额");
            }
            // 损益类科目年初余额恒 0，禁止录入非 0 期初（0 期初允许跳过）
            if ((item.DebitBalance != 0 || item.CreditBalance != 0)
                && accountInfoMap.TryGetValue(item.AccountId, out var info)
                && FinAccountCategory.IsProfitLoss(info.FCategory))
            {
                throw new InvalidOperationException($"损益类科目（{info.FCode}）不可录入期初余额");
            }
        }

        // 试算平衡校验（全量快照：末级科目期初取 提交值→库中现值→0，Σ借 必须 == Σ贷）
        var leafAccountIds = accountIds.Where(id => !parentIds.Contains(id)).ToList();
        var existingBalanceRows = await _balanceRepository.Query()
            .Where(b => b.FPeriodId == 0 && b.FAccountSetId == request.AccountSetId)
            .ToListAsync();
        var existingMap = existingBalanceRows
            .GroupBy(b => b.FAccountId)
            .ToDictionary(g => g.Key, g => (g.Sum(b => b.FBeginDebit), g.Sum(b => b.FBeginCredit)));
        var submittedMap = request.Items
            .GroupBy(i => i.AccountId)
            .ToDictionary(g => g.Key, g => (g.Sum(i => i.DebitBalance), g.Sum(i => i.CreditBalance)));
        var (totalDebit, totalCredit) = InitialBalanceValidator.ComputeTotals(leafAccountIds, existingMap, submittedMap);
        var difference = totalDebit - totalCredit;
        if (!InitialBalanceValidator.IsBalanced(difference))
        {
            throw new InvalidOperationException(
                $"期初借贷不平衡，借方合计 {totalDebit:F2}，贷方合计 {totalCredit:F2}，差额 {difference:F2}");
        }

        foreach (var item in request.Items)
        {
            // 期初余额使用 PeriodId=0 存储
            var existing = await _balanceRepository.Query()
                .FirstOrDefaultAsync(b => b.FPeriodId == 0 && b.FAccountId == item.AccountId && b.FAccountSetId == request.AccountSetId);

            if (existing != null)
            {
                existing.FBeginDebit = item.DebitBalance;
                existing.FBeginCredit = item.CreditBalance;
                // 期末 = 期初 + 本期发生，避免覆盖已有发生额
                existing.FEndDebit = item.DebitBalance + existing.FCurrentDebit;
                existing.FEndCredit = item.CreditBalance + existing.FCurrentCredit;
                existing.FUpdatedTime = DateTime.Now;
                await _balanceRepository.UpdateAsync(existing);
            }
            else
            {
                var balance = new FinAccountBalance
                {
                    FPeriodId = 0,
                    FAccountId = item.AccountId,
                    FAccountSetId = request.AccountSetId,
                    FBeginDebit = item.DebitBalance,
                    FBeginCredit = item.CreditBalance,
                    FCurrentDebit = 0,
                    FCurrentCredit = 0,
                    FEndDebit = item.DebitBalance,
                    FEndCredit = item.CreditBalance,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };
                await _balanceRepository.AddAsync(balance);
            }
        }
        return true;
    }

    public async Task<List<AccountDto>> GetByAuxTypeAsync(string auxType, long accountSetId)
    {
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId
                && a.FAuxiliary != null
                && ("," + a.FAuxiliary + ",").Contains("," + auxType + ",")
                && a.FEnableStatus == 1)
            .OrderBy(a => a.FCode)
            .ToListAsync();
        return accounts.Select(MapToDto).ToList();
    }

    public async Task<bool> UpdateAccountAuxiliaryAsync(UpdateAccountAuxiliaryRequest request)
    {
        // 获取该账套下所有启用科目
        var allAccounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == request.AccountSetId && a.FEnableStatus == 1)
            .ToListAsync();

        var checkedCodes = new HashSet<string>(request.AccountCodes);
        var changed = new List<FinAccount>();

        foreach (var account in allAccounts)
        {
            var currentAux = account.FAuxiliary ?? string.Empty;
            var parts = currentAux.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(p => p.Trim())
                                  .Where(p => !string.IsNullOrEmpty(p))
                                  .ToList();

            bool hasType = parts.Contains(request.AuxType);
            bool shouldHave = checkedCodes.Contains(account.FCode);

            if (shouldHave && !hasType)
            {
                // 添加类别（统一使用英文编码写入）
                parts.Add(request.AuxType);
                account.FAuxiliary = string.Join(",", parts);
                account.FUpdatedTime = DateTime.Now;
                changed.Add(account);
            }
            else if (!shouldHave && hasType)
            {
                // 移除类别
                parts.Remove(request.AuxType);
                account.FAuxiliary = parts.Count > 0 ? string.Join(",", parts) : null;
                account.FUpdatedTime = DateTime.Now;
                changed.Add(account);
            }
        }

        foreach (var account in changed)
        {
            await _accountRepository.UpdateAsync(account);
        }

        return true;
    }

    private static List<AccountTreeDto> BuildTree(List<FinAccount> accounts)
    {
        var lookup = accounts.ToLookup(a => a.FParentId);
        
        AccountTreeDto BuildNode(FinAccount account)
        {
            var node = new AccountTreeDto
            {
                Id = account.FID,
                Code = account.FCode,
                Name = account.FName,
                Category = account.FCategory,
                BalanceDirection = account.FBalanceDirection,
                Level = account.FLevel,
                ParentId = account.FParentId,
                IsLeaf = account.FIsLeaf == 1,
                Auxiliary = account.FAuxiliary,
                Currency = account.FCurrency,
                Unit = account.FUnit,
                EnableStatus = account.FEnableStatus == 1
            };
            
            node.Children = lookup[account.FID].Select(BuildNode).ToList();
            return node;
        }
        
        return lookup[0].Select(BuildNode).ToList();
    }

    private static AccountDto MapToDto(FinAccount account)
    {
        return new AccountDto
        {
            Id = account.FID,
            Code = account.FCode,
            Name = account.FName,
            Category = account.FCategory,
            BalanceDirection = account.FBalanceDirection,
            Level = account.FLevel,
            ParentId = account.FParentId,
            IsLeaf = account.FIsLeaf == 1,
            Auxiliary = account.FAuxiliary,
            Currency = account.FCurrency,
            Unit = account.FUnit,
            EnableStatus = account.FEnableStatus == 1
        };
    }
}
