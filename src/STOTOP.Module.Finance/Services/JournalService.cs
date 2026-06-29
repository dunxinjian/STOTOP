using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Constants;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Services;

public class JournalService
{
    private readonly IRepository<FinVoucher> _voucherRepository;
    private readonly IRepository<FinVoucherEntry> _entryRepository;
    private readonly IRepository<FinAccount> _accountRepository;
    private readonly IRepository<FinAccountBalance> _balanceRepository;
    private readonly IRepository<FinAccountPeriod> _periodRepository;
    private readonly OperationLogService _operationLogService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<JournalService> _logger;

    public JournalService(
        IRepository<FinVoucher> voucherRepository,
        IRepository<FinVoucherEntry> entryRepository,
        IRepository<FinAccount> accountRepository,
        IRepository<FinAccountBalance> balanceRepository,
        IRepository<FinAccountPeriod> periodRepository,
        OperationLogService operationLogService,
        IHttpContextAccessor httpContextAccessor,
        STOTOPDbContext dbContext,
        ILogger<JournalService> logger)
    {
        _voucherRepository = voucherRepository;
        _entryRepository = entryRepository;
        _accountRepository = accountRepository;
        _balanceRepository = balanceRepository;
        _periodRepository = periodRepository;
        _operationLogService = operationLogService;
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _logger = logger;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    // ─────────────────────────────────────────────────────────
    // 1. 全部日记账
    // ─────────────────────────────────────────────────────────
    public async Task<JournalPagedResult> GetJournalEntriesAsync(JournalQueryRequest request)
    {
        var (startDate, endDate) = GetDateRange(request);

        var entryQuery = BuildBaseEntryQuery(request.AccountSetId, startDate, endDate);

        // 按科目过滤
        if (!string.IsNullOrWhiteSpace(request.AccountCode))
        {
            var code = request.AccountCode;
            entryQuery = entryQuery.Where(e => e.FAccountCode.StartsWith(code));
        }

        // 按科目类别过滤（通过子查询在数据库层执行）
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category;
            var categoryAccountIds = _accountRepository.Query()
                .Where(a => a.FAccountSetId == request.AccountSetId && a.FCategory == category)
                .Select(a => a.FID);
            entryQuery = entryQuery.Where(e => categoryAccountIds.Contains(e.FAccountId));
        }

        // 文本搜索
        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText;
            entryQuery = request.SearchField switch
            {
                "accountCode" => entryQuery.Where(e => e.FAccountCode.Contains(text)),
                "accountName" => entryQuery.Where(e => e.FAccountName.Contains(text)),
                "summary"     => entryQuery.Where(e => e.FSummary.Contains(text)),
                _             => entryQuery.Where(e =>
                    e.FSummary.Contains(text) ||
                    e.FAccountCode.Contains(text) ||
                    e.FAccountName.Contains(text))
            };
        }

        var total = await entryQuery.CountAsync();

        // 分页（先拿到内存再组装）
        var rawItems = await ExecuteBaseQueryAsync(request.AccountSetId, startDate, endDate,
            q =>
            {
                var q2 = q;
                if (!string.IsNullOrWhiteSpace(request.AccountCode))
                {
                    var code = request.AccountCode;
                    q2 = q2.Where(e => e.FAccountCode.StartsWith(code));
                }
                if (!string.IsNullOrWhiteSpace(request.Category))
                {
                    var category = request.Category;
                    var categoryAccountIds = _accountRepository.Query()
                        .Where(a => a.FAccountSetId == request.AccountSetId && a.FCategory == category)
                        .Select(a => a.FID);
                    q2 = q2.Where(e => categoryAccountIds.Contains(e.FAccountId));
                }
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    var text = request.SearchText;
                    q2 = request.SearchField switch
                    {
                        "accountCode" => q2.Where(e => e.FAccountCode.Contains(text)),
                        "accountName" => q2.Where(e => e.FAccountName.Contains(text)),
                        "summary"     => q2.Where(e => e.FSummary.Contains(text)),
                        _             => q2.Where(e =>
                            e.FSummary.Contains(text) ||
                            e.FAccountCode.Contains(text) ||
                            e.FAccountName.Contains(text))
                    };
                }
                return q2;
            });

        var sorted = rawItems
            .OrderBy(x => x.Voucher.FDate)
            .ThenBy(x => x.Voucher.FVoucherNo)
            .ThenBy(x => x.Entry.FLineNo);

        var items = sorted
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => MapToDto(x.Entry, x.Voucher, x.Account))
            .ToList();

        return new JournalPagedResult
        {
            Items = items,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    // ─────────────────────────────────────────────────────────
    // 2. 现金银行日记账（1001% OR 1002%）
    // ─────────────────────────────────────────────────────────
    public async Task<JournalPagedResult> GetCashBankJournalAsync(JournalQueryRequest request)
    {
        var (startDate, endDate) = GetDateRange(request);
        
        var total = await BuildBaseEntryQuery(request.AccountSetId, startDate, endDate)
            .Where(e => e.FAccountCode.StartsWith("1001") || e.FAccountCode.StartsWith("1002"))
            .CountAsync();
        
        var rawItems = await ExecuteBaseQueryAsync(request.AccountSetId, startDate, endDate,
            q => q.Where(e => e.FAccountCode.StartsWith("1001") || e.FAccountCode.StartsWith("1002")));
        
        // 计算期初余额
        decimal initialBalance = await GetInitialBalanceAsync(
            request.AccountSetId, startDate,
            new[] { "1001", "1002" });
        
        var entries = rawItems
            .OrderBy(x => x.Voucher.FDate)
            .ThenBy(x => x.Voucher.FVoucherNo)
            .ThenBy(x => x.Entry.FLineNo)
            .Select(x => MapToDto(x.Entry, x.Voucher, x.Account))
            .ToList();
        ComputeRunningBalance(entries, initialBalance, "借", prepend: true);
        
        // 分页
        var paged = entries
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new JournalPagedResult
        {
            Items = paged,
            Total = total + 1, // 包含期初行
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    // ─────────────────────────────────────────────────────────
    // 3. 应收应付日记账（1122% OR 2202%）
    // ─────────────────────────────────────────────────────────
    public async Task<JournalPagedResult> GetReceivablePayableJournalAsync(JournalQueryRequest request)
    {
        var (startDate, endDate) = GetDateRange(request);

        var total = await BuildBaseEntryQuery(request.AccountSetId, startDate, endDate)
            .Where(e => e.FAccountCode.StartsWith("1122") || e.FAccountCode.StartsWith("2202"))
            .CountAsync();

        var rawItems = await ExecuteBaseQueryAsync(request.AccountSetId, startDate, endDate,
            q => q.Where(e => e.FAccountCode.StartsWith("1122") || e.FAccountCode.StartsWith("2202")));

        // 1122 应收（借方余额），2202 应付（贷方余额）
        decimal initialBalance = await GetInitialBalanceAsync(
            request.AccountSetId, startDate,
            new[] { "1122", "2202" });

        var entries = rawItems
            .OrderBy(x => x.Voucher.FDate)
            .ThenBy(x => x.Voucher.FVoucherNo)
            .ThenBy(x => x.Entry.FLineNo)
            .Select(x =>
            {
                var dto = MapToDto(x.Entry, x.Voucher, x.Account);
                dto.Direction = x.Account.FCode.StartsWith("1122") ? "应收" : "应付";
                return dto;
            })
            .ToList();

        ComputeRunningBalance(entries, initialBalance, "借", prepend: true);

        var paged = entries
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new JournalPagedResult
        {
            Items = paged,
            Total = total + 1,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    // ─────────────────────────────────────────────────────────
    // 4. 创建调整凭证
    // ─────────────────────────────────────────────────────────
    public async Task<long> AdjustAsync(JournalAdjustRequest request)
    {
        var account = await _accountRepository.GetByIdAsync(request.AccountId)
            ?? throw new InvalidOperationException($"科目 {request.AccountId} 不存在");

        // 查找或创建所属期间
        var period = await _periodRepository.Query()
            .FirstOrDefaultAsync(p =>
                p.FAccountSetId == request.AccountSetId &&
                p.FYear == request.Date.Year &&
                p.FPeriodNo == request.Date.Month)
            ?? throw new InvalidOperationException($"未找到 {request.Date.Year}年{request.Date.Month}期 的会计期间");

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // 使用悲观锁查询最大凭证号
                var maxNo = await _dbContext.Database.SqlQueryRaw<int?>(
                    @"SELECT MAX([F凭证号]) AS [Value] FROM [FIN凭证] WITH (UPDLOCK, HOLDLOCK)
                      WHERE [F凭证字] = @word AND [F期间ID] = @periodId AND [F账套ID] = @accountSetId",
                    new SqlParameter("@word", VoucherWord.Ji),
                    new SqlParameter("@periodId", period.FID),
                    new SqlParameter("@accountSetId", request.AccountSetId)
                ).FirstOrDefaultAsync() ?? 0;

                var voucher = new FinVoucher
                {
                    FVoucherWord = VoucherWord.Ji,
                    FVoucherNo = maxNo + 1,
                    FDate = request.Date,
                    FPeriodId = period.FID,
                    FAttachmentCount = 0,
                    FCreator = "journal",
                    FStatus = request.SaveAsDraft ? (int)VoucherStatus.Draft : (int)VoucherStatus.Pending,
                    FSource = "journal:adjust",
                    FRemark = request.Summary,
                    FAccountSetId = request.AccountSetId,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };

                await _voucherRepository.AddAsync(voucher);

                // 调整分录：借/贷 + 对方科目（同科目反方向，形成自平衡）
                bool isDebit = string.Equals(request.Direction, "debit", StringComparison.OrdinalIgnoreCase);

                var entry1 = new FinVoucherEntry
                {
                    FVoucherId = voucher.FID,
                    FLineNo = 1,
                    FSummary = request.Summary,
                    FAccountId = account.FID,
                    FAccountCode = account.FCode,
                    FAccountName = account.FName,
                    FDebitAmount = isDebit ? request.Amount : 0,
                    FCreditAmount = isDebit ? 0 : request.Amount,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };

                var entry2 = new FinVoucherEntry
                {
                    FVoucherId = voucher.FID,
                    FLineNo = 2,
                    FSummary = request.Summary,
                    FAccountId = account.FID,
                    FAccountCode = account.FCode,
                    FAccountName = account.FName,
                    FDebitAmount = isDebit ? 0 : request.Amount,
                    FCreditAmount = isDebit ? request.Amount : 0,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };

                await _entryRepository.AddAsync(entry1);
                await _entryRepository.AddAsync(entry2);

                await transaction.CommitAsync();

                await _operationLogService.LogAsync(
                    request.AccountSetId, "日记账", "调整",
                    $"日记账调整凭证 记{voucher.FVoucherNo}",
                    voucher.FID, $"记{voucher.FVoucherNo}");

                return voucher.FID;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < maxRetries)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("凭证号冲突，第{Attempt}次重试", attempt);
                continue;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        throw new InvalidOperationException("凭证号生成失败：多次重试后仍存在冲突");
    }

    // ─────────────────────────────────────────────────────────
    // 5. 将草稿凭证提交（状态 0 → 1）
    // ─────────────────────────────────────────────────────────
    public async Task<bool> GenerateVoucherAsync(JournalGenerateVoucherRequest request)
    {
        if (!request.EntryIds.Any())
            return false;

        // 根据分录找到凭证
        var entries = await _entryRepository.Query()
            .Where(e => request.EntryIds.Contains(e.FID))
            .ToListAsync();

        var voucherIds = entries.Select(e => e.FVoucherId).Distinct().ToList();

        var vouchers = await _voucherRepository.Query()
            .Where(v => voucherIds.Contains(v.FID) && v.FAccountSetId == request.AccountSetId)
            .ToListAsync();

        foreach (var voucher in vouchers.Where(v => v.FStatus == (int)VoucherStatus.Draft))
        {
            voucher.FStatus = (int)VoucherStatus.Pending;
            voucher.FUpdatedTime = DateTime.Now;
            await _voucherRepository.UpdateAsync(voucher);
        }

        return true;
    }

    // ─────────────────────────────────────────────────────────
    // 6. 删除凭证（仅草稿/待审核）
    // ─────────────────────────────────────────────────────────
    public async Task<bool> DeleteVoucherAsync(long voucherId, long accountSetId)
    {
        var voucher = await _voucherRepository.Query()
            .FirstOrDefaultAsync(v => v.FID == voucherId && v.FAccountSetId == accountSetId);

        if (voucher == null) return false;

        if (voucher.FStatus == (int)VoucherStatus.Audited)
            throw new InvalidOperationException("已审核的凭证不能删除");

        await _voucherRepository.DeleteAsync(voucherId);
        await _operationLogService.LogAsync(
            accountSetId, "日记账", "删除",
            $"日记账删除凭证 {voucher.FVoucherWord}{voucher.FVoucherNo}",
            voucherId, $"{voucher.FVoucherWord}{voucher.FVoucherNo}");
        return true;
    }

    // ─────────────────────────────────────────────────────────
    // 7. 新增日记账（收入 / 支出 / 收支）
    // ─────────────────────────────────────────────────────────
    public async Task<long> CreateAsync(JournalCreateRequest request)
    {
        // 查找会计期间
        var period = await _periodRepository.Query()
            .FirstOrDefaultAsync(p =>
                p.FAccountSetId == request.AccountSetId &&
                p.FYear == request.Date.Year &&
                p.FPeriodNo == request.Date.Month)
            ?? throw new InvalidOperationException(
                $"未找到 {request.Date.Year}年{request.Date.Month}期 的会计期间");

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // 使用悲观锁查询最大凭证号
                var maxNo = await _dbContext.Database.SqlQueryRaw<int?>(
                    @"SELECT MAX([F凭证号]) AS [Value] FROM [FIN凭证] WITH (UPDLOCK, HOLDLOCK)
                      WHERE [F凭证字] = @word AND [F期间ID] = @periodId AND [F账套ID] = @accountSetId",
                    new SqlParameter("@word", VoucherWord.Ji),
                    new SqlParameter("@periodId", period.FID),
                    new SqlParameter("@accountSetId", request.AccountSetId)
                ).FirstOrDefaultAsync() ?? 0;

                var voucher = new FinVoucher
                {
                    FVoucherWord    = VoucherWord.Ji,
                    FVoucherNo      = maxNo + 1,
                    FDate           = request.Date,
                    FPeriodId       = period.FID,
                    FAttachmentCount = request.AttachmentCount,
                    FCreator        = "journal",
                    FStatus         = request.SaveAsDraft ? (int)VoucherStatus.Draft : (int)VoucherStatus.Pending,
                    FSource         = "journal:create",
                    FRemark         = request.Summary,
                    FAccountSetId   = request.AccountSetId,
                    FCreatedTime    = DateTime.Now,
                    FUpdatedTime    = DateTime.Now
                };

                await _voucherRepository.AddAsync(voucher);

                // 根据类型生成分录对
                var entries = await BuildEntriesAsync(request, voucher.FID);

                int lineNo = 1;
                foreach (var e in entries)
                {
                    e.FLineNo = lineNo++;
                    e.FCreatedTime = DateTime.Now;
                    e.FUpdatedTime = DateTime.Now;
                    await _entryRepository.AddAsync(e);
                }

                await transaction.CommitAsync();

                await _operationLogService.LogAsync(
                    request.AccountSetId, "日记账", "新增",
                    $"新增日记账凭证 记{voucher.FVoucherNo}",
                    voucher.FID, $"记{voucher.FVoucherNo}");

                return voucher.FID;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < maxRetries)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("凭证号冲突，第{Attempt}次重试", attempt);
                continue;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        throw new InvalidOperationException("凭证号生成失败：多次重试后仍存在冲突");
    }

    /// <summary>
    /// 根据 JournalCreateRequest 构建分录列表（不设 FLineNo / 时间戳，由调用方填充）
    /// </summary>
    private async Task<List<FinVoucherEntry>> BuildEntriesAsync(JournalCreateRequest req, long voucherId)
    {
        var type = req.Type.ToLowerInvariant();

        return type switch
        {
            "income"   => await BuildIncomeEntriesAsync(req, voucherId),
            "expense"  => await BuildExpenseEntriesAsync(req, voucherId),
            "transfer" => await BuildTransferEntriesAsync(req, voucherId),
            _ => throw new InvalidOperationException($"未知日记账类型: {req.Type}")
        };
    }

    /// <summary>
    /// 收入：借记现金銀行账户，贷记收入科目（由 Category 映射）
    /// 支持多账户模式：AccountItems 有值时，为每个账户生成一条借方分录
    /// </summary>
    private async Task<List<FinVoucherEntry>> BuildIncomeEntriesAsync(
        JournalCreateRequest req, long voucherId)
    {
        // 贷方：收入科目（按 Category 自动定位）
        var incomeAccount = await ResolveIncomeAccountAsync(req.AccountSetId, req.Category);
    
        // 多账户模式
        if (req.AccountItems != null && req.AccountItems.Count > 0)
        {
            var entries = new List<FinVoucherEntry>();
            decimal totalAmount = 0m;
            foreach (var item in req.AccountItems)
            {
                var cashAccount = await _accountRepository.GetByIdAsync(item.AccountId)
                    ?? throw new InvalidOperationException($"账户科目 {item.AccountId} 不存在");
                entries.Add(MakeEntry(voucherId, req.Summary, cashAccount, debit: item.Amount, credit: 0));
                totalAmount += item.Amount;
            }
            // 贷方收入科目合并一条
            entries.Add(MakeEntry(voucherId, req.Summary, incomeAccount, debit: 0, credit: totalAmount));
            return entries;
        }
    
        // 单账户模式（兼容旧逻辑）
        var singleCashAccount = await _accountRepository.GetByIdAsync(req.AccountId)
            ?? throw new InvalidOperationException($"账户科目 {req.AccountId} 不存在");
    
        return new List<FinVoucherEntry>
        {
            MakeEntry(voucherId, req.Summary, singleCashAccount,  debit: req.Amount, credit: 0),
            MakeEntry(voucherId, req.Summary, incomeAccount, debit: 0, credit: req.Amount)
        };
    }

    /// <summary>
    /// 支出：借记费用科目（由 Category 映射），贷记现金銀行账户
    /// 支持多账户模式：AccountItems 有值时，为每个账户生成一条贷方分录
    /// </summary>
    private async Task<List<FinVoucherEntry>> BuildExpenseEntriesAsync(
        JournalCreateRequest req, long voucherId)
    {
        // 借方：费用科目（按 Category 自动定位）
        var expenseAccount = await ResolveExpenseAccountAsync(req.AccountSetId, req.Category);
    
        // 多账户模式
        if (req.AccountItems != null && req.AccountItems.Count > 0)
        {
            var entries = new List<FinVoucherEntry>();
            decimal totalAmount = 0m;
            foreach (var item in req.AccountItems)
            {
                var cashAccount = await _accountRepository.GetByIdAsync(item.AccountId)
                    ?? throw new InvalidOperationException($"账户科目 {item.AccountId} 不存在");
                entries.Add(MakeEntry(voucherId, req.Summary, cashAccount, debit: 0, credit: item.Amount));
                totalAmount += item.Amount;
            }
            // 借方费用科目合并一条
            entries.Insert(0, MakeEntry(voucherId, req.Summary, expenseAccount, debit: totalAmount, credit: 0));
            return entries;
        }
    
        // 单账户模式（兼容旧逻辑）
        var singleCashAccount = await _accountRepository.GetByIdAsync(req.AccountId)
            ?? throw new InvalidOperationException($"账户科目 {req.AccountId} 不存在");
    
        return new List<FinVoucherEntry>
        {
            MakeEntry(voucherId, req.Summary, expenseAccount, debit: req.Amount, credit: 0),
            MakeEntry(voucherId, req.Summary, singleCashAccount,    debit: 0, credit: req.Amount)
        };
    }

    /// <summary>
    /// 收支：收付款 or 内部转账
    /// </summary>
    private async Task<List<FinVoucherEntry>> BuildTransferEntriesAsync(
        JournalCreateRequest req, long voucherId)
    {
        var subType = (req.SubType ?? "payment").ToLowerInvariant();

        if (subType == "internal-transfer")
            return await BuildInternalTransferEntriesAsync(req, voucherId);

        return await BuildPaymentEntriesAsync(req, voucherId);
    }

    /// <summary>
    /// 内部转账：借记目标银行账户，贷记源银行账户（AccountId）
    /// </summary>
    private async Task<List<FinVoucherEntry>> BuildInternalTransferEntriesAsync(
        JournalCreateRequest req, long voucherId)
    {
        if (!req.TransferToAccountId.HasValue)
            throw new InvalidOperationException("内部转账需要指定目标账户 TransferToAccountId");

        var fromAccount = await _accountRepository.GetByIdAsync(req.AccountId)
            ?? throw new InvalidOperationException($"源账户科目 {req.AccountId} 不存在");

        var toAccount = await _accountRepository.GetByIdAsync(req.TransferToAccountId.Value)
            ?? throw new InvalidOperationException($"目标账户科目 {req.TransferToAccountId} 不存在");

        return new List<FinVoucherEntry>
        {
            MakeEntry(voucherId, req.Summary, toAccount,   debit: req.Amount, credit: 0),
            MakeEntry(voucherId, req.Summary, fromAccount, debit: 0, credit: req.Amount)
        };
    }

    /// <summary>
    /// 收付款核销：
    ///   应收核销 — 借记现金银行账户，贷记 1122 应收账款
    ///   应付核销 — 借记 2202 应付账款，贷记现金银行账户
    /// </summary>
    private async Task<List<FinVoucherEntry>> BuildPaymentEntriesAsync(
        JournalCreateRequest req, long voucherId)
    {
        var direction = (req.ReconcileDirection ?? "receivable").ToLowerInvariant();
        var amount    = req.ReconcileAmount ?? req.Amount;

        // 核销账户（1122 或 2202 子科目）
        if (!req.ReconcileAccountId.HasValue)
            throw new InvalidOperationException("收付款核销需要指定 ReconcileAccountId");

        var reconcileAccount = await _accountRepository.GetByIdAsync(req.ReconcileAccountId.Value)
            ?? throw new InvalidOperationException($"核销账户 {req.ReconcileAccountId} 不存在");

        // 现金银行账户（1001/1002 子科目）
        if (!req.CashBankAccountId.HasValue)
            throw new InvalidOperationException("收付款核销需要指定 CashBankAccountId");

        var cashAccount = await _accountRepository.GetByIdAsync(req.CashBankAccountId.Value)
            ?? throw new InvalidOperationException($"现金银行账户 {req.CashBankAccountId} 不存在");

        if (direction == "receivable")
        {
            // 应收核销：借现金银行，贷应收账款
            return new List<FinVoucherEntry>
            {
                MakeEntry(voucherId, req.Summary, cashAccount,      debit: amount, credit: 0),
                MakeEntry(voucherId, req.Summary, reconcileAccount, debit: 0,      credit: amount)
            };
        }
        else
        {
            // 应付核销：借应付账款，贷现金银行
            return new List<FinVoucherEntry>
            {
                MakeEntry(voucherId, req.Summary, reconcileAccount, debit: amount, credit: 0),
                MakeEntry(voucherId, req.Summary, cashAccount,      debit: 0,      credit: amount)
            };
        }
    }

    // ─────────────────────────────────────────────────────────
    // 收入 / 费用科目自动映射
    // ─────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> IncomeCategoryMap = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["主营业务"]   = "5001",
        ["其他业务"]   = "5051",
        ["投资收益"]   = "5111",
        ["营业外收入"] = "5301",
    };

    private static readonly Dictionary<string, string> ExpenseCategoryMap = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["营业成本"]   = "5401",
        ["销售费用"]   = "5601",
        ["管理费用"]   = "5602",
        ["财务费用"]   = "5603",
        ["营业外支出"] = "5711",
    };

    private async Task<FinAccount> ResolveIncomeAccountAsync(long accountSetId, string? category)
    {
        var prefix = (category != null && IncomeCategoryMap.TryGetValue(category, out var p))
            ? p : "5001";

        return await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId && a.FCode.StartsWith(prefix))
            .OrderBy(a => a.FCode)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                $"未找到收入科目（前缀 {prefix}），请先在科目表中维护");
    }

    private async Task<FinAccount> ResolveExpenseAccountAsync(long accountSetId, string? category)
    {
        var prefix = (category != null && ExpenseCategoryMap.TryGetValue(category, out var p))
            ? p : "5602";

        return await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId && a.FCode.StartsWith(prefix))
            .OrderBy(a => a.FCode)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                $"未找到费用科目（前缀 {prefix}），请先在科目表中维护");
    }

    // ─────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────

    private record JournalRawRow(
        FinVoucherEntry Entry,
        FinVoucher Voucher,
        FinAccount Account);

    /// <summary>
    /// 返回可组合进一步筛选的底层查询。
    /// 注意：由于 EF Core 无法投影到自定义 record，返回 IQueryable&lt;JournalRawRow&gt; 使用客户端汇之前先 .CountAsync() 。
    /// 实际数据通过 ExecuteBaseQueryAsync 加载至内存。
    /// </summary>
    private IQueryable<FinVoucherEntry> BuildBaseEntryQuery(
        long accountSetId, DateTime startDate, DateTime endDate)
    {
        return from entry in _entryRepository.Query()
               join voucher in _voucherRepository.Query() on entry.FVoucherId equals voucher.FID
               where voucher.FAccountSetId == accountSetId
                     && voucher.FDate >= startDate
                     && voucher.FDate <= endDate
               select entry;
    }

    private async Task<List<JournalRawRow>> ExecuteBaseQueryAsync(
        long accountSetId, DateTime startDate, DateTime endDate,
        Func<IQueryable<FinVoucherEntry>, IQueryable<FinVoucherEntry>>? entryFilter = null)
    {
        var entryQuery = BuildBaseEntryQuery(accountSetId, startDate, endDate);
        if (entryFilter != null) entryQuery = entryFilter(entryQuery);

        var entries = await entryQuery.ToListAsync();
        var entryIds   = entries.Select(e => e.FVoucherId).Distinct().ToList();
        var accountIds = entries.Select(e => e.FAccountId).Distinct().ToList();

        var vouchers = await _voucherRepository.Query()
            .Where(v => entryIds.Contains(v.FID))
            .ToListAsync();

        var accounts = await _accountRepository.Query()
            .Where(a => accountIds.Contains(a.FID))
            .ToListAsync();

        var voucherMap = vouchers.ToDictionary(v => v.FID);
        var accountMap = accounts.ToDictionary(a => a.FID);

        return entries
            .Where(e => voucherMap.ContainsKey(e.FVoucherId) && accountMap.ContainsKey(e.FAccountId))
            .Select(e => new JournalRawRow(e, voucherMap[e.FVoucherId], accountMap[e.FAccountId]))
            .ToList();
    }

    /// <summary>构建一条凭证分录（FLineNo / 时间戳由调用方填充）</summary>
    private static FinVoucherEntry MakeEntry(
        long voucherId, string summary, FinAccount account,
        decimal debit, decimal credit) => new()
    {
        FVoucherId   = voucherId,
        FSummary     = summary,
        FAccountId   = account.FID,
        FAccountCode = account.FCode,
        FAccountName = account.FName,
        FDebitAmount  = debit,
        FCreditAmount = credit
    };

    private (DateTime startDate, DateTime endDate) GetDateRange(JournalQueryRequest request)
    {
        return request.QueryMode switch
        {
            "period" when request.Year.HasValue && request.Month.HasValue =>
                (new DateTime(request.Year.Value, request.Month.Value, 1),
                 new DateTime(request.Year.Value, request.Month.Value, 1).AddMonths(1).AddDays(-1)),

            "date" when request.StartDate.HasValue =>
                (request.StartDate.Value.Date,
                 request.EndDate.HasValue ? request.EndDate.Value.Date : request.StartDate.Value.Date),

            "period-range" when request.StartYear.HasValue && request.StartMonth.HasValue
                             && request.EndYear.HasValue && request.EndMonth.HasValue =>
                (new DateTime(request.StartYear.Value, request.StartMonth.Value, 1),
                 new DateTime(request.EndYear.Value, request.EndMonth.Value, 1).AddMonths(1).AddDays(-1)),

            "date-range" when request.StartDate.HasValue && request.EndDate.HasValue =>
                (request.StartDate.Value.Date, request.EndDate.Value.Date),

            _ when request.StartDate.HasValue && request.EndDate.HasValue =>
                (request.StartDate.Value.Date, request.EndDate.Value.Date),

            _ when request.Year.HasValue && request.Month.HasValue =>
                (new DateTime(request.Year.Value, request.Month.Value, 1),
                 new DateTime(request.Year.Value, request.Month.Value, 1).AddMonths(1).AddDays(-1)),

            _ => (DateTime.Today.AddDays(1 - DateTime.Today.Day),
                  new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1))
        };
    }

    /// <summary>
    /// 查询期初余额（startDate 之前所有已过账凭证的累计余额）
    /// </summary>
    private async Task<decimal> GetInitialBalanceAsync(long accountSetId, DateTime startDate, string[] codePrefixes)
    {
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();

        var targetAccountIds = accounts
            .Where(a => codePrefixes.Any(p => a.FCode.StartsWith(p)))
            .Select(a => a.FID)
            .ToHashSet();

        if (!targetAccountIds.Any()) return 0m;

        // 查所有 startDate 之前的已过账分录
        var priorEntries = await (from e in _entryRepository.Query()
                                  join v in _voucherRepository.Query() on e.FVoucherId equals v.FID
                                  where v.FAccountSetId == accountSetId
                                        && v.FStatus == (int)VoucherStatus.Audited
                                        && v.FDate < startDate
                                        && targetAccountIds.Contains(e.FAccountId)
                                  select e).ToListAsync();

        // 取第一个匹配科目的余额方向（大多数情况下同一批科目方向一致）
        var firstAccount = accounts.FirstOrDefault(a => targetAccountIds.Contains(a.FID));
        var direction = firstAccount?.FBalanceDirection ?? "借";

        decimal totalDebit = priorEntries.Sum(e => e.FDebitAmount);
        decimal totalCredit = priorEntries.Sum(e => e.FCreditAmount);

        return direction == "借" ? (totalDebit - totalCredit) : (totalCredit - totalDebit);
    }

    /// <summary>
    /// 逐行计算滚动余额，并可选插入期初行
    /// </summary>
    private static void ComputeRunningBalance(List<JournalEntryDto> entries, decimal initialBalance, string balanceDirection, bool prepend)
    {
        if (prepend)
        {
            entries.Insert(0, new JournalEntryDto
            {
                IsInitialBalance = true,
                Summary = "期初余额",
                Balance = initialBalance,
                Direction = balanceDirection
            });
        }

        decimal running = initialBalance;
        for (int i = prepend ? 1 : 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (balanceDirection == "借")
                running += e.DebitAmount - e.CreditAmount;
            else
                running += e.CreditAmount - e.DebitAmount;
            e.Balance = running;
        }
    }

    private static JournalEntryDto MapToDto(FinVoucherEntry entry, FinVoucher voucher, FinAccount account)
    {
        return new JournalEntryDto
        {
            Id = entry.FID,
            VoucherId = voucher.FID,
            Date = voucher.FDate,
            Summary = entry.FSummary,
            Category = account.FCategory,
            AccountCode = account.FCode,
            AccountName = account.FName,
            DebitAmount = entry.FDebitAmount,
            CreditAmount = entry.FCreditAmount,
            Direction = account.FBalanceDirection,
            VoucherNo = $"{voucher.FVoucherWord}-{voucher.FVoucherNo:D4}",
            VoucherStatus = voucher.FStatus,
            IsInitialBalance = false
        };
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx &&
               (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }
}
