using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Events;
using STOTOP.Module.Finance.Constants;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class AccountPeriodService : IAccountPeriodService
{
    private readonly IRepository<FinAccountPeriod> _periodRepository;
    private readonly IRepository<FinVoucher> _voucherRepository;
    private readonly IRepository<FinVoucherEntry> _voucherEntryRepository;
    private readonly IRepository<FinAccount> _accountRepository;
    private readonly IRepository<FinAccountBalance> _balanceRepository;
    private readonly OperationLogService _operationLogService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITrialBalanceService _trialBalanceService;
    private readonly IReportService _reportService;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<AccountPeriodService> _logger;
    private readonly STOTOPDbContext _context;
    private readonly IAccountSetRuleService _accountSetRuleService;

    public AccountPeriodService(
        IRepository<FinAccountPeriod> periodRepository,
        IRepository<FinVoucher> voucherRepository,
        IRepository<FinVoucherEntry> voucherEntryRepository,
        IRepository<FinAccount> accountRepository,
        IRepository<FinAccountBalance> balanceRepository,
        OperationLogService operationLogService,
        IHttpContextAccessor httpContextAccessor,
        ITrialBalanceService trialBalanceService,
        IReportService reportService,
        IEventDispatcher eventDispatcher,
        ILogger<AccountPeriodService> logger,
        STOTOPDbContext context,
        IAccountSetRuleService accountSetRuleService)
    {
        _periodRepository = periodRepository;
        _voucherRepository = voucherRepository;
        _voucherEntryRepository = voucherEntryRepository;
        _accountRepository = accountRepository;
        _balanceRepository = balanceRepository;
        _operationLogService = operationLogService;
        _httpContextAccessor = httpContextAccessor;
        _trialBalanceService = trialBalanceService;
        _reportService = reportService;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
        _context = context;
        _accountSetRuleService = accountSetRuleService;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    private long GetCurrentUserId()
    {
        var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (claim != null && long.TryParse(claim.Value, out var userId))
            return userId;
        return 0;
    }

    public async Task<List<AccountPeriodDto>> GetAllAsync(long accountSetId = 0)
    {
        var periods = await _periodRepository.Query()
            .Where(p => p.FAccountSetId == accountSetId)
            .OrderByDescending(p => p.FYear)
            .ThenByDescending(p => p.FPeriodNo)
            .ToListAsync();
        
        return periods.Select(MapToDto).ToList();
    }

    public async Task<List<AccountPeriodDto>> GetByYearAsync(int year, long accountSetId = 0)
    {
        var periods = await _periodRepository.Query()
            .Where(p => p.FYear == year && p.FAccountSetId == accountSetId)
            .OrderBy(p => p.FPeriodNo)
            .ToListAsync();
        
        return periods.Select(MapToDto).ToList();
    }

    public async Task<AccountPeriodDto?> GetCurrentAsync(long accountSetId = 0)
    {
        // 用当天零点比较：FEndDate 存的是当月末日 00:00:00（CreateYearPeriodsAsync），
        // 若与带时刻的 DateTime.Now 比较，月末当天 00:00 之后 FEndDate >= now 恒为 false → 当月最后一天查不到当前期间。
        var today = DateTime.Now.Date;
        var period = await _periodRepository.Query()
            .Where(p => p.FStartDate <= today && p.FEndDate >= today && p.FStatus == 1 && p.FAccountSetId == accountSetId)
            .FirstOrDefaultAsync();
        
        return period == null ? null : MapToDto(period);
    }

    public async Task<AccountPeriodDto?> GetByIdAsync(long id)
    {
        var period = await _periodRepository.GetByIdAsync(id);
        return period == null ? null : MapToDto(period);
    }

    public async Task<List<AccountPeriodDto>> CreateYearPeriodsAsync(int year, long accountSetId = 0)
    {
        var existingPeriods = await _periodRepository.Query()
            .Where(p => p.FYear == year && p.FAccountSetId == accountSetId)
            .ToListAsync();
        
        if (existingPeriods.Any())
        {
            throw new InvalidOperationException($"{year}年的会计期间已存在");
        }

        var periods = new List<FinAccountPeriod>();
        for (int i = 1; i <= 12; i++)
        {
            var startDate = new DateTime(year, i, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            
            var period = new FinAccountPeriod
            {
                FYear = year,
                FPeriodNo = i,
                FStartDate = startDate,
                FEndDate = endDate,
                FIsClosed = 0,
                FStatus = 1,
                FAccountSetId = accountSetId,
                FOrgId = GetCurrentOrgId(),
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            
            periods.Add(await _periodRepository.AddAsync(period));
        }

        // 确保所有期间数据持久化
        await _context.SaveChangesAsync();
        
        return periods.Select(MapToDto).ToList();
    }

    public async Task<(bool success, string message)> CloseAsync(long periodId, long accountSetId = 0)
    {
        // 1. 验证前置条件
        var period = await _periodRepository.GetByIdAsync(periodId);
        if (period == null) return (false, "期间不存在");
        // 期间须属于传入账套：GetByIdAsync 按主键取，绕过账套维度，
        // 若 periodId 与 accountSetId 错配则后续全部校验/结转会落到另一账套的数据上（IDOR + 数据污染）。
        if (period.FAccountSetId != accountSetId) return (false, "期间不属于该账套");
        if (period.FIsClosed == 1) return (false, "该期间已结账");

        // 检查上一期间是否已结账（该账套首期除外）：非1月查同年上一期，1月查上一年12月。
        FinAccountPeriod? prevPeriod;
        if (period.FPeriodNo > 1)
        {
            prevPeriod = await _periodRepository.Query()
                .Where(p => p.FAccountSetId == accountSetId && p.FYear == period.FYear && p.FPeriodNo == period.FPeriodNo - 1)
                .FirstOrDefaultAsync();
        }
        else
        {
            // 1月：查上一年12月（该账套上一年度存在期间时才要求先结账；不存在=账套首期，放行）
            prevPeriod = await _periodRepository.Query()
                .Where(p => p.FAccountSetId == accountSetId && p.FYear == period.FYear - 1 && p.FPeriodNo == 12)
                .FirstOrDefaultAsync();
        }
        if (prevPeriod != null && prevPeriod.FIsClosed != 1)
            return (false, $"请先结账 {prevPeriod.FYear}年{prevPeriod.FPeriodNo}月");

        // 检查无待审核凭证（与 PreCloseCheck 口径一致：仅待审核 FStatus==1 阻断；
        // 草稿(0)未提交、作废(-1)已死、锁定(3)已入账，均不计入，避免作废凭证永久阻塞结账）。
        var unauditedCount = await _voucherRepository.Query()
            .CountAsync(v => v.FPeriodId == periodId && v.FAccountSetId == accountSetId && v.FStatus == 1);
        if (unauditedCount > 0)
            return (false, $"该期间有{unauditedCount}张待审核凭证，请先审核");

        // 从余额重算 → 结转凭证生成 → 期初结转 → 锁定 → 标记结账 全程一个显式事务：
        // 中途任一步异常须整体回滚，否则会留半截结账（如结转凭证已生成但期间未标记、或期初已结转但凭证未锁定）。
        // Repository.Add/Update 内部各自 SaveChangesAsync，被此外层事务统一提交/回滚；
        // RecalculateBalanceAsync 无嵌套事务，纳入范围安全。
        using var _closeTx = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
        try
        {
        // 先基于当前已审凭证重算期末余额，再做试算平衡校验。
        // 否则 GenerateTrialBalanceAsync 读取的是尚未重算（可能为空或为上次结账的旧值）的科目余额表，
        // 空表会得到 借0==贷0 的"假平衡"，使这道强制校验形同虚设。
        await _reportService.RecalculateBalanceAsync(periodId, accountSetId);

        // 结账前强制试算平衡检查
        var trialBalance = await _trialBalanceService.GenerateTrialBalanceAsync(periodId, accountSetId);
        if (!trialBalance.IsBalanced)
        {
            if (_closeTx != null) await _closeTx.RollbackAsync();
            return (false, $"试算不平衡，借方合计 {trialBalance.TotalDebit}，贷方合计 {trialBalance.TotalCredit}，差额 {Math.Abs(trialBalance.TotalDebit - trialBalance.TotalCredit)}");
        }

        // 2. 查找本年利润科目（P0-2 结转目标科目按账套规则读取，无配置回退 3103/310405）
        var (profitCode, retainedCode) = await _accountSetRuleService.GetClosingAccountCodesAsync(accountSetId);
        var profitAccount = await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.FCode == profitCode && a.FAccountSetId == accountSetId);
        if (profitAccount == null)
        {
            if (_closeTx != null) await _closeTx.RollbackAsync();
            return (false, $"未找到{profitCode}(本年利润)科目，无法结账");
        }

        // 3. 查询所有损益类末级科目，汇总本期发生额并生成结转凭证
        var profitAndLossAccounts = await _accountRepository.Query()
            .Where(a => FinAccountCategory.ProfitLossCategories.Contains(a.FCategory) && a.FIsLeaf == 1 && a.FAccountSetId == accountSetId)
            .ToListAsync();

        var periodVoucherIds = await _voucherRepository.Query()
            .Where(v => v.FPeriodId == periodId && v.FAccountSetId == accountSetId && v.FStatus == 2)
            .Select(v => v.FID)
            .ToListAsync();

        var entries = await _voucherEntryRepository.Query()
            .Where(e => periodVoucherIds.Contains(e.FVoucherId))
            .ToListAsync();

        var closingEntries = new List<FinVoucherEntry>();
        decimal totalProfitDebit = 0;
        decimal totalProfitCredit = 0;
        int lineNo = 1;

        foreach (var account in profitAndLossAccounts)
        {
            var accountEntries = entries.Where(e => e.FAccountId == account.FID);
            var debitSum = accountEntries.Sum(e => e.FDebitAmount);
            var creditSum = accountEntries.Sum(e => e.FCreditAmount);

            if (debitSum == 0 && creditSum == 0) continue;

            if (account.FBalanceDirection == "贷") // 收入类
            {
                var netAmount = creditSum - debitSum; // 正常为正数（净收入）
                if (netAmount != 0)
                {
                    // 借记收入科目（冲平），差额贷记本年利润
                    closingEntries.Add(new FinVoucherEntry
                    {
                        FLineNo = lineNo++,
                        FSummary = "结转收入",
                        FAccountId = account.FID,
                        FAccountCode = account.FCode,
                        FAccountName = account.FName,
                        FDebitAmount = netAmount > 0 ? netAmount : 0,
                        FCreditAmount = netAmount < 0 ? -netAmount : 0,
                        FCreatedTime = DateTime.Now,
                        FUpdatedTime = DateTime.Now
                    });
                    if (netAmount > 0) totalProfitCredit += netAmount;
                    else totalProfitDebit += -netAmount;
                }
            }
            else // 费用类（借方余额）
            {
                var netAmount = debitSum - creditSum; // 正常为正数（净费用）
                if (netAmount != 0)
                {
                    // 贷记费用科目（冲平），差额借记本年利润
                    closingEntries.Add(new FinVoucherEntry
                    {
                        FLineNo = lineNo++,
                        FSummary = "结转费用",
                        FAccountId = account.FID,
                        FAccountCode = account.FCode,
                        FAccountName = account.FName,
                        FDebitAmount = netAmount < 0 ? -netAmount : 0,
                        FCreditAmount = netAmount > 0 ? netAmount : 0,
                        FCreatedTime = DateTime.Now,
                        FUpdatedTime = DateTime.Now
                    });
                    if (netAmount > 0) totalProfitDebit += netAmount;
                    else totalProfitCredit += -netAmount;
                }
            }
        }

        // 添加本年利润对方分录（保持借贷平衡）
        if (closingEntries.Count > 0)
        {
            closingEntries.Add(new FinVoucherEntry
            {
                FLineNo = lineNo++,
                FSummary = "结转本年利润",
                FAccountId = profitAccount.FID,
                FAccountCode = profitAccount.FCode,
                FAccountName = profitAccount.FName,
                FDebitAmount = totalProfitDebit,   // 费用净额（本年利润借方）
                FCreditAmount = totalProfitCredit, // 收入净额（本年利润贷方）
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            });

            var maxNo = await _voucherRepository.Query()
                .Where(v => v.FPeriodId == periodId && v.FAccountSetId == accountSetId)
                .MaxAsync(v => (int?)v.FVoucherNo) ?? 0;

            var closingVoucher = new FinVoucher
            {
                FVoucherWord = "转",
                FVoucherNo = maxNo + 1,
                FDate = period.FEndDate,
                FPeriodId = periodId,
                FAttachmentCount = 0,
                FCreator = "系统",
                FAuditor = "系统",
                FStatus = 2, // 已审核
                FSource = "system:closing",
                FRemark = $"{period.FYear}年{period.FPeriodNo}月损益结转",
                FAccountSetId = accountSetId,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now,
                Entries = closingEntries
            };

            await _voucherRepository.AddAsync(closingVoucher);
        }

        // 4. 12月额外处理：本年利润 → 未分配利润
        if (period.FPeriodNo == 12)
        {
            var retainedAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.FCode == retainedCode && a.FAccountSetId == accountSetId);
            if (retainedAccount != null)
            {
                // 计算3103全年累计余额（贷-借的净额）
                var yearPeriodIds = await _periodRepository.Query()
                    .Where(p => p.FAccountSetId == accountSetId && p.FYear == period.FYear)
                    .Select(p => p.FID)
                    .ToListAsync();

                var yearVoucherIds = await _voucherRepository.Query()
                    .Where(v => yearPeriodIds.Contains(v.FPeriodId) && v.FAccountSetId == accountSetId && v.FStatus == 2)
                    .Select(v => v.FID)
                    .ToListAsync();

                var profitEntries = await _voucherEntryRepository.Query()
                    .Where(e => yearVoucherIds.Contains(e.FVoucherId) && e.FAccountId == profitAccount.FID)
                    .ToListAsync();

                var yearProfit = profitEntries.Sum(e => e.FCreditAmount) - profitEntries.Sum(e => e.FDebitAmount);

                if (yearProfit != 0)
                {
                    var maxNo2 = await _voucherRepository.Query()
                        .Where(v => v.FPeriodId == periodId && v.FAccountSetId == accountSetId)
                        .MaxAsync(v => (int?)v.FVoucherNo) ?? 0;

                    var yearEndEntries = new List<FinVoucherEntry>();
                    if (yearProfit > 0) // 盈利：借本年利润，贷未分配利润
                    {
                        yearEndEntries.Add(new FinVoucherEntry { FLineNo = 1, FSummary = "结转全年利润", FAccountId = profitAccount.FID, FAccountCode = profitAccount.FCode, FAccountName = profitAccount.FName, FDebitAmount = yearProfit, FCreditAmount = 0, FCreatedTime = DateTime.Now, FUpdatedTime = DateTime.Now });
                        yearEndEntries.Add(new FinVoucherEntry { FLineNo = 2, FSummary = "结转全年利润", FAccountId = retainedAccount.FID, FAccountCode = retainedAccount.FCode, FAccountName = retainedAccount.FName, FDebitAmount = 0, FCreditAmount = yearProfit, FCreatedTime = DateTime.Now, FUpdatedTime = DateTime.Now });
                    }
                    else // 亏损：借未分配利润，贷本年利润
                    {
                        yearEndEntries.Add(new FinVoucherEntry { FLineNo = 1, FSummary = "结转全年亏损", FAccountId = retainedAccount.FID, FAccountCode = retainedAccount.FCode, FAccountName = retainedAccount.FName, FDebitAmount = -yearProfit, FCreditAmount = 0, FCreatedTime = DateTime.Now, FUpdatedTime = DateTime.Now });
                        yearEndEntries.Add(new FinVoucherEntry { FLineNo = 2, FSummary = "结转全年亏损", FAccountId = profitAccount.FID, FAccountCode = profitAccount.FCode, FAccountName = profitAccount.FName, FDebitAmount = 0, FCreditAmount = -yearProfit, FCreatedTime = DateTime.Now, FUpdatedTime = DateTime.Now });
                    }

                    var yearEndVoucher = new FinVoucher
                    {
                        FVoucherWord = "转",
                        FVoucherNo = maxNo2 + 1,
                        FDate = period.FEndDate,
                        FPeriodId = periodId,
                        FAttachmentCount = 0,
                        FCreator = "系统",
                        FAuditor = "系统",
                        FStatus = 2,
                        FSource = "system:closing",
                        FRemark = $"{period.FYear}年度利润结转",
                        FAccountSetId = accountSetId,
                        FCreatedTime = DateTime.Now,
                        FUpdatedTime = DateTime.Now,
                        Entries = yearEndEntries
                    };

                    await _voucherRepository.AddAsync(yearEndVoucher);
                }
            }
        }

        // 结转凭证（步骤3/4 新增的 status=2 损益/利润结转分录）已入账，须先重算本期余额，
        // 否则下一步读到的是结转前旧余额：损益科目未清零、本年利润缺本期利润 → 下期期初错误。
        await _reportService.RecalculateBalanceAsync(periodId, accountSetId);

        // 5. 结转下期期初余额
        var nextPeriod = await _periodRepository.Query()
            .Where(p => p.FAccountSetId == accountSetId
                && ((p.FYear == period.FYear && p.FPeriodNo == period.FPeriodNo + 1)
                    || (period.FPeriodNo == 12 && p.FYear == period.FYear + 1 && p.FPeriodNo == 1)))
            .FirstOrDefaultAsync();

        if (nextPeriod != null)
        {
            var currentBalances = await _balanceRepository.Query()
                .Where(b => b.FPeriodId == periodId && b.FAccountSetId == accountSetId)
                .ToListAsync();

            foreach (var balance in currentBalances)
            {
                var nextBalance = await _balanceRepository.Query()
                    .FirstOrDefaultAsync(b => b.FPeriodId == nextPeriod.FID && b.FAccountId == balance.FAccountId);

                if (nextBalance == null)
                {
                    nextBalance = new FinAccountBalance
                    {
                        FPeriodId = nextPeriod.FID,
                        FAccountId = balance.FAccountId,
                        FAccountSetId = accountSetId,
                        FBeginDebit = balance.FEndDebit,
                        FBeginCredit = balance.FEndCredit,
                        FCurrentDebit = 0,
                        FCurrentCredit = 0,
                        FEndDebit = balance.FEndDebit,
                        FEndCredit = balance.FEndCredit,
                        FCreatedTime = DateTime.Now,
                        FUpdatedTime = DateTime.Now
                    };
                    await _balanceRepository.AddAsync(nextBalance);
                }
                else
                {
                    nextBalance.FBeginDebit = balance.FEndDebit;
                    nextBalance.FBeginCredit = balance.FEndCredit;
                    nextBalance.FUpdatedTime = DateTime.Now;
                    await _balanceRepository.UpdateAsync(nextBalance);
                }
            }
        }

        // 6. 锁定该期间所有凭证（FStatus = 3）
        var vouchersToLock = await _voucherRepository.Query()
            .Where(v => v.FPeriodId == periodId && v.FAccountSetId == accountSetId)
            .ToListAsync();
        foreach (var v in vouchersToLock)
        {
            v.FStatus = 3;
            v.FUpdatedTime = DateTime.Now;
            await _voucherRepository.UpdateAsync(v);
        }

        // 7. 标记结账
        period.FIsClosed = 1;
        period.FUpdatedTime = DateTime.Now;
        await _periodRepository.UpdateAsync(period);

        await _operationLogService.LogAsync(
            accountSetId, "结账", "结账",
            $"结账 {period.FYear}年{period.FPeriodNo}月期间",
            periodId, $"{period.FYear}-{period.FPeriodNo:D2}");

        // 结账后预计算报表缓存
        await _reportService.RecalculateBalanceAsync(periodId, accountSetId);

        if (_closeTx != null) await _closeTx.CommitAsync();
        }
        catch
        {
            if (_closeTx != null) await _closeTx.RollbackAsync();
            throw;
        }

        // 发布账期关闭事件（事务已提交，事件发布失败不回滚已结账结果）
        try
        {
            await _eventDispatcher.PublishAsync(new AccountPeriodClosedEvent
            {
                PeriodId = periodId,
                PeriodName = $"{period.FYear}年第{period.FPeriodNo}期",
                AccountSetId = accountSetId,
                TriggeredByUserId = GetCurrentUserId(),
                ModuleCode = "finance"
            });
        }
        catch (Exception ex)
        {
            // 事件发布失败不影响主业务
            _logger?.LogError(ex, "发布AccountPeriodClosedEvent失败");
        }

        return (true, "结账成功");
    }

    public async Task<(bool success, string message)> ReopenAsync(long periodId, long accountSetId = 0)
    {
        var period = await _periodRepository.GetByIdAsync(periodId);
        if (period == null) return (false, "期间不存在");
        // 期间须属于传入账套（同 CloseAsync：GetByIdAsync 绕过账套维度，错配会解锁他账套期间/凭证）。
        if (period.FAccountSetId != accountSetId) return (false, "期间不属于该账套");
        if (period.FIsClosed != 1) return (false, "该期间未结账");

        // 检查下一期间未结账
        var nextPeriod = await _periodRepository.Query()
            .Where(p => p.FAccountSetId == accountSetId
                && ((p.FYear == period.FYear && p.FPeriodNo == period.FPeriodNo + 1)
                    || (period.FPeriodNo == 12 && p.FYear == period.FYear + 1 && p.FPeriodNo == 1)))
            .FirstOrDefaultAsync();
        if (nextPeriod != null && nextPeriod.FIsClosed == 1)
            return (false, "请先反结账下一期间");

        // 删除结转凭证 → 解锁凭证 → 清结账标志 全程一个显式事务，中途异常整体回滚，避免半截反结账。
        using var _reopenTx = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
        try
        {
        // 删除系统结转凭证
        var closingVouchers = await _voucherRepository.Query()
            .Where(v => v.FPeriodId == periodId && v.FAccountSetId == accountSetId && v.FSource == "system:closing")
            .ToListAsync();

        foreach (var voucher in closingVouchers)
        {
            var voucherEntries = await _voucherEntryRepository.Query()
                .Where(e => e.FVoucherId == voucher.FID)
                .ToListAsync();
            foreach (var entry in voucherEntries)
                await _voucherEntryRepository.DeleteAsync(entry.FID);
            await _voucherRepository.DeleteAsync(voucher.FID);
        }

        // 解锁该期间凭证（恢复为已审核状态 FStatus = 2）
        var vouchersToUnlock = await _voucherRepository.Query()
            .Where(v => v.FPeriodId == periodId && v.FAccountSetId == accountSetId && v.FStatus == 3)
            .ToListAsync();
        foreach (var v in vouchersToUnlock)
        {
            v.FStatus = 2;
            v.FUpdatedTime = DateTime.Now;
            await _voucherRepository.UpdateAsync(v);
        }

        // 清除结账标志
        period.FIsClosed = 0;
        period.FUpdatedTime = DateTime.Now;
        await _periodRepository.UpdateAsync(period);

        await _operationLogService.LogAsync(
            accountSetId, "反结账", "反结账",
            $"反结账 {period.FYear}年{period.FPeriodNo}月期间",
            periodId, $"{period.FYear}-{period.FPeriodNo:D2}");

        if (_reopenTx != null) await _reopenTx.CommitAsync();
        }
        catch
        {
            if (_reopenTx != null) await _reopenTx.RollbackAsync();
            throw;
        }

        return (true, "反结账成功");
    }

    public async Task<object> PreCloseCheckAsync(long accountSetId, int year, int periodNo)
    {
        var messages = new List<string>();

        // 1. 找到对应期间
        var period = await _periodRepository.Query()
            .FirstOrDefaultAsync(p => p.FAccountSetId == accountSetId && p.FYear == year && p.FPeriodNo == periodNo);

        if (period == null)
        {
            return new
            {
                canClose = false,
                unauditedCount = 0,
                unbalancedCount = 0,
                unbalancedVoucherNos = new List<int>(),
                messages = new List<string> { $"{year}年{periodNo}月期间不存在" }
            };
        }

        var periodId = period.FID;

        // 2. 查询未审核凭证数量（FStatus != 2 且非草稿）
        var unauditedCount = await _voucherRepository.Query()
            .CountAsync(v => v.FPeriodId == periodId && v.FAccountSetId == accountSetId && v.FStatus == 1);

        if (unauditedCount > 0)
            messages.Add($"有 {unauditedCount} 张凭证未审核，请先全部审核后再结账");

        // 3. 检查借贷不平衡的凭证
        var vouchersWithEntries = await _voucherRepository.Query()
            .Include(v => v.Entries)
            .Where(v => v.FPeriodId == periodId && v.FAccountSetId == accountSetId && v.FStatus != 0)
            .ToListAsync();

        var unbalancedVoucherNos = vouchersWithEntries
            .Where(v =>
            {
                var totalDebit = v.Entries.Sum(e => e.FDebitAmount);
                var totalCredit = v.Entries.Sum(e => e.FCreditAmount);
                return Math.Abs(totalDebit - totalCredit) > 0.001m;
            })
            .Select(v => v.FVoucherNo)
            .ToList();

        if (unbalancedVoucherNos.Count > 0)
            messages.Add($"有 {unbalancedVoucherNos.Count} 张凭证借贷不平衡，凭证号：{string.Join(", ", unbalancedVoucherNos)}");

        var canClose = unauditedCount == 0 && unbalancedVoucherNos.Count == 0;

        return new
        {
            canClose,
            unauditedCount,
            unbalancedCount = unbalancedVoucherNos.Count,
            unbalancedVoucherNos,
            messages
        };
    }

    private static AccountPeriodDto MapToDto(FinAccountPeriod period)
    {
        return new AccountPeriodDto
        {
            Id = period.FID,
            Year = period.FYear,
            PeriodNo = period.FPeriodNo,
            StartDate = period.FStartDate,
            EndDate = period.FEndDate,
            IsClosed = period.FIsClosed == 1,
            Status = period.FStatus
        };
    }
}
