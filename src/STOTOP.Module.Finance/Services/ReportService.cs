using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Constants;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.FormulaEngine;
using STOTOP.Module.Finance.Services.Interfaces;
using global::System.Text.Json;
using global::System.Text.RegularExpressions;

namespace STOTOP.Module.Finance.Services;

public class ReportService : IReportService
{
    private readonly IRepository<FinAccountBalance> _balanceRepository;
    private readonly IRepository<FinAuxiliaryBalance> _auxiliaryBalanceRepository;
    private readonly IRepository<FinAccount> _accountRepository;
    private readonly IRepository<FinVoucherEntry> _voucherEntryRepository;
    private readonly IRepository<FinVoucher> _voucherRepository;
    private readonly IRepository<FinAssetCard> _assetCardRepository;
    private readonly IRepository<FinAssetCategory> _assetCategoryRepository;
    private readonly IRepository<FinAmoebaPLTemplate> _templateRepository;
    private readonly IRepository<FinAccountPeriod> _periodRepository;
    private readonly IRepository<FinReportFormula> _formulaRepository;
    private readonly IFormulaEngine _formulaEngine;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReportService(
        IRepository<FinAccountBalance> balanceRepository,
        IRepository<FinAuxiliaryBalance> auxiliaryBalanceRepository,
        IRepository<FinAccount> accountRepository,
        IRepository<FinVoucherEntry> voucherEntryRepository,
        IRepository<FinVoucher> voucherRepository,
        IRepository<FinAssetCard> assetCardRepository,
        IRepository<FinAssetCategory> assetCategoryRepository,
        IRepository<FinAmoebaPLTemplate> templateRepository,
        IRepository<FinAccountPeriod> periodRepository,
        IRepository<FinReportFormula> formulaRepository,
        IFormulaEngine formulaEngine,
        IHttpContextAccessor httpContextAccessor)
    {
        _balanceRepository = balanceRepository;
        _auxiliaryBalanceRepository = auxiliaryBalanceRepository;
        _accountRepository = accountRepository;
        _voucherEntryRepository = voucherEntryRepository;
        _voucherRepository = voucherRepository;
        _assetCardRepository = assetCardRepository;
        _assetCategoryRepository = assetCategoryRepository;
        _templateRepository = templateRepository;
        _periodRepository = periodRepository;
        _formulaRepository = formulaRepository;
        _formulaEngine = formulaEngine;
        _httpContextAccessor = httpContextAccessor;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    public async Task<List<AccountBalanceDto>> GetAccountBalanceAsync(long periodId, long? accountId = null, long accountSetId = 0)
    {
        var query = _balanceRepository.Query()
            .Where(b => b.FPeriodId == periodId && b.FAccountSetId == accountSetId);
        
        if (accountId.HasValue)
        {
            query = query.Where(b => b.FAccountId == accountId.Value);
        }

        var balances = await query.ToListAsync();
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();

        return balances.Select(b =>
        {
            var account = accounts.FirstOrDefault(a => a.FID == b.FAccountId);
            return new AccountBalanceDto
            {
                AccountId = b.FAccountId,
                AccountCode = account?.FCode ?? "",
                AccountName = account?.FName ?? "",
                Category = account?.FCategory ?? "",
                Level = account?.FLevel ?? 0,
                BeginDebit = b.FBeginDebit,
                BeginCredit = b.FBeginCredit,
                CurrentDebit = b.FCurrentDebit,
                CurrentCredit = b.FCurrentCredit,
                EndDebit = b.FEndDebit,
                EndCredit = b.FEndCredit
            };
        }).OrderBy(b => b.AccountCode).ToList();
    }

    /// <summary>
    /// 从凭证实时计算科目余额表（按年月）
    /// </summary>
    public async Task<List<AccountBalanceDto>> GetAccountBalanceByYearMonthAsync(int year, int month, long? accountId = null, long accountSetId = 0)
    {
        // 获取会计期间
        var targetPeriod = await _periodRepository.Query()
            .FirstOrDefaultAsync(p => p.FYear == year && p.FPeriodNo == month && p.FAccountSetId == accountSetId);
        
        if (targetPeriod == null)
        {
            return new List<AccountBalanceDto>();
        }

        // 获取所有科目
        var accountsQuery = _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId);
        if (accountId.HasValue)
        {
            accountsQuery = accountsQuery.Where(a => a.FID == accountId.Value);
        }
        var accounts = await accountsQuery.OrderBy(a => a.FCode).ToListAsync();

        // 获取本期及之前的所有已过账凭证分录
        var entries = await (from e in _voucherEntryRepository.Query()
                             join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                             join p in _periodRepository.Query().Where(p => p.FAccountSetId == accountSetId) on v.FPeriodId equals p.FID
                             where (v.FStatus == 2 || v.FStatus == 3) && p.FYear == year && p.FPeriodNo <= month
                             select new { e, p.FPeriodNo }).ToListAsync();

        var result = new List<AccountBalanceDto>();

        foreach (var account in accounts)
        {
            // 计算期初余额（当前期间之前的所有凭证累计）
            var beginEntries = entries.Where(x => x.FPeriodNo < month && x.e.FAccountId == account.FID);
            var beginDebit = beginEntries.Sum(x => x.e.FDebitAmount);
            var beginCredit = beginEntries.Sum(x => x.e.FCreditAmount);

            // 计算本期发生额
            var currentEntries = entries.Where(x => x.FPeriodNo == month && x.e.FAccountId == account.FID);
            var currentDebit = currentEntries.Sum(x => x.e.FDebitAmount);
            var currentCredit = currentEntries.Sum(x => x.e.FCreditAmount);

            // 根据科目余额方向计算期初和期末余额
            var (beginDebitBalance, beginCreditBalance) = CalculateBalance(
                beginDebit, beginCredit, account.FBalanceDirection);
            
            var (endDebitBalance, endCreditBalance) = CalculateEndBalance(
                beginDebitBalance, beginCreditBalance, currentDebit, currentCredit, account.FBalanceDirection);

            result.Add(new AccountBalanceDto
            {
                AccountId = account.FID,
                AccountCode = account.FCode,
                AccountName = account.FName,
                Category = account.FCategory,
                Level = account.FLevel,
                BeginDebit = beginDebitBalance,
                BeginCredit = beginCreditBalance,
                CurrentDebit = currentDebit,
                CurrentCredit = currentCredit,
                EndDebit = endDebitBalance,
                EndCredit = endCreditBalance
            });
        }

        // 添加合计行
        if (result.Count > 0)
        {
            result.Add(new AccountBalanceDto
            {
                AccountId = 0,
                AccountCode = "",
                AccountName = "合计",
                Category = "",
                Level = 0,
                BeginDebit = result.Sum(r => r.BeginDebit),
                BeginCredit = result.Sum(r => r.BeginCredit),
                CurrentDebit = result.Sum(r => r.CurrentDebit),
                CurrentCredit = result.Sum(r => r.CurrentCredit),
                EndDebit = result.Sum(r => r.EndDebit),
                EndCredit = result.Sum(r => r.EndCredit)
            });
        }

        return result;
    }

    /// <summary>
    /// 计算余额（根据科目余额方向）
    /// </summary>
    private (decimal debit, decimal credit) CalculateBalance(decimal totalDebit, decimal totalCredit, string balanceDirection)
    {
        if (balanceDirection == "借")
        {
            // 借方余额科目：余额 = 借方累计 - 贷方累计
            var balance = totalDebit - totalCredit;
            return balance >= 0 ? (balance, 0m) : (0m, -balance);
        }
        else
        {
            // 贷方余额科目：余额 = 贷方累计 - 借方累计
            var balance = totalCredit - totalDebit;
            return balance >= 0 ? (0m, balance) : (-balance, 0m);
        }
    }

    /// <summary>
    /// 计算期末余额
    /// </summary>
    private (decimal debit, decimal credit) CalculateEndBalance(
        decimal beginDebit, decimal beginCredit, 
        decimal currentDebit, decimal currentCredit, 
        string balanceDirection)
    {
        if (balanceDirection == "借")
        {
            // 借方余额科目：期末 = 期初借方 - 期初贷方 + 本期借方 - 本期贷方
            var balance = beginDebit - beginCredit + currentDebit - currentCredit;
            return balance >= 0 ? (balance, 0m) : (0m, -balance);
        }
        else
        {
            // 贷方余额科目：期末 = 期初贷方 - 期初借方 + 本期贷方 - 本期借方
            var balance = beginCredit - beginDebit + currentCredit - currentDebit;
            return balance >= 0 ? (0m, balance) : (-balance, 0m);
        }
    }

    private static string FormatAuxiliaryInfo(string? auxiliaryJson)
    {
        if (string.IsNullOrWhiteSpace(auxiliaryJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(auxiliaryJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return auxiliaryJson;
            }

            var parts = document.RootElement.EnumerateObject()
                .Select(property => FormatAuxiliaryValue(property.Value))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            return parts.Count > 0 ? string.Join(" / ", parts) : auxiliaryJson;
        }
        catch
        {
            return auxiliaryJson;
        }
    }

    private static string FormatAuxiliaryValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object when element.TryGetProperty("name", out var nameLower) => nameLower.GetString() ?? string.Empty,
            JsonValueKind.Object when element.TryGetProperty("Name", out var nameUpper) => nameUpper.GetString() ?? string.Empty,
            JsonValueKind.Object when element.TryGetProperty("label", out var label) => label.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join(" / ", element.EnumerateArray().Select(FormatAuxiliaryValue).Where(v => !string.IsNullOrWhiteSpace(v))),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "是",
            JsonValueKind.False => "否",
            _ => string.Empty
        };
    }

    public async Task<List<AuxiliaryBalanceDto>> GetAuxiliaryBalanceAsync(long periodId, string? auxiliaryType = null, long accountSetId = 0)
    {
        var query = _auxiliaryBalanceRepository.Query()
            .Where(b => b.FPeriodId == periodId && b.FAccountSetId == accountSetId);

        if (!string.IsNullOrWhiteSpace(auxiliaryType))
        {
            var type = auxiliaryType.Trim();
            query = query.Where(b => b.FAuxiliaryJson.Contains(type));
        }

        var balances = await query.ToListAsync();
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();

        return balances.Select(b =>
        {
            var account = accounts.FirstOrDefault(a => a.FID == b.FAccountId);
            var auxiliaryInfo = FormatAuxiliaryInfo(b.FAuxiliaryJson);
            return new AuxiliaryBalanceDto
            {
                AccountId = b.FAccountId,
                AccountCode = account?.FCode ?? "",
                AccountName = account?.FName ?? "",
                AuxiliaryJson = b.FAuxiliaryJson,
                AuxiliaryInfo = auxiliaryInfo,
                OpeningDebit = b.FBeginDebit,
                OpeningCredit = b.FBeginCredit,
                PeriodDebit = b.FCurrentDebit,
                PeriodCredit = b.FCurrentCredit,
                ClosingDebit = b.FEndDebit,
                ClosingCredit = b.FEndCredit,
                BeginDebit = b.FBeginDebit,
                BeginCredit = b.FBeginCredit,
                CurrentDebit = b.FCurrentDebit,
                CurrentCredit = b.FCurrentCredit,
                EndDebit = b.FEndDebit,
                EndCredit = b.FEndCredit
            };
        }).ToList();
    }

    public async Task<List<AssetBalanceDto>> GetAssetBalanceAsync(long? periodId = null, long accountSetId = 0)
    {
        var query = _assetCardRepository.Query()
            .Where(c => c.FStatus == 1 && c.FAccountSetId == accountSetId);

        if (periodId.HasValue)
        {
            var period = await _periodRepository.Query()
                .FirstOrDefaultAsync(p => p.FID == periodId.Value && p.FAccountSetId == accountSetId);
            if (period == null)
            {
                return new List<AssetBalanceDto>();
            }

            query = query.Where(c => c.FEntryDate <= period.FEndDate);
        }

        var cards = await query
            .OrderBy(c => c.FCode)
            .ToListAsync();

        var categories = await _assetCategoryRepository.Query()
            .Where(c => c.FAccountSetId == accountSetId)
            .ToListAsync();

        return cards.Select(c =>
            {
                var category = categories.FirstOrDefault(category => category.FID == c.FCategoryId);
                return new AssetBalanceDto
                {
                    AssetId = c.FID,
                    AssetCode = c.FCode,
                    AssetName = c.FName,
                    CategoryId = c.FCategoryId,
                    CategoryName = category?.FName ?? "未分类",
                    DepartmentId = c.FDepartmentId,
                    DepartmentName = c.FDepartmentId.HasValue ? c.FDepartmentId.Value.ToString() : null,
                    OriginalValue = c.FOriginalValue,
                    AccumulatedDepreciation = c.FAccumulatedDepreciation,
                    NetValue = c.FNetValue,
                    AssetCount = 1,
                    OriginalValueTotal = c.FOriginalValue,
                    AccumulatedDepreciationTotal = c.FAccumulatedDepreciation,
                    NetValueTotal = c.FNetValue
                };
            }).ToList();
    }

    public async Task<List<ProfitStatementDto>> GetProfitStatementAsync(long startPeriodId, long endPeriodId, string format = "small", long accountSetId = 0)
    {
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();

        // 获取选定期间的日期范围
        var startPeriod = await _periodRepository.GetByIdAsync(startPeriodId);
        var endPeriod = await _periodRepository.GetByIdAsync(endPeriodId);
        if (startPeriod == null || endPeriod == null)
        {
            return new List<ProfitStatementDto>();
        }

        // 本期日期范围
        var currentStartDate = new DateTime(startPeriod.FYear, startPeriod.FPeriodNo, 1);
        var currentEndDate = new DateTime(endPeriod.FYear, endPeriod.FPeriodNo, 1).AddMonths(1).AddDays(-1);

        // 本年累计日期范围（从年初到 endPeriod）
        var yearStartDate = new DateTime(endPeriod.FYear, 1, 1);
        var yearEndDate = currentEndDate;

        // 获取本期已过账凭证分录
        var currentEntries = await (from e in _voucherEntryRepository.Query()
                                     join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                                     where (v.FStatus == 2 || v.FStatus == 3) && v.FSource != VoucherSource.SystemClosing && v.FDate >= currentStartDate && v.FDate <= currentEndDate
                                     select e).ToListAsync();

        // 获取本年累计已过账凭证分录
        var yearEntries = await (from e in _voucherEntryRepository.Query()
                                 join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                                 where (v.FStatus == 2 || v.FStatus == 3) && v.FSource != VoucherSource.SystemClosing && v.FDate >= yearStartDate && v.FDate <= yearEndDate
                                 select e).ToListAsync();

        // 使用与小企业报表相同的实时计算逻辑
        var currentAmounts = CalculateAccountAmounts(currentEntries, accounts);
        var yearAmounts = CalculateAccountAmounts(yearEntries, accounts);

        // 尝试从数据库加载公式
        var formulas = await _formulaRepository.Query()
            .Where(f => f.FReportType == "利润表" && f.FAccountSetId == accountSetId && f.FIsEnabled)
            .OrderBy(f => f.FRowIndex)
            .ToListAsync();

        if (formulas.Count > 0)
        {
            return BuildProfitStatementFromFormulas(formulas, currentAmounts, yearAmounts);
        }

        // Fallback: 硬编码逻辑
        return BuildProfitStatementHardcoded(currentAmounts, yearAmounts);
    }

    /// <summary>
    /// 公式驱动的利润表构建
    /// </summary>
    private List<ProfitStatementDto> BuildProfitStatementFromFormulas(
        List<FinReportFormula> formulas,
        Dictionary<string, decimal> currentAmounts,
        Dictionary<string, decimal> yearAmounts)
    {
        var curContext = new FormulaContext { AccountAmounts = currentAmounts };
        var yearContext = new FormulaContext { AccountAmounts = yearAmounts };
        var result = new List<ProfitStatementDto>();

        foreach (var formula in formulas)
        {
            if (string.IsNullOrEmpty(formula.FFormula) || formula.FFormulaType == "header")
            {
                result.Add(new ProfitStatementDto
                {
                    ItemName = formula.FItemName,
                    RowIndex = formula.FRowIndex
                });
                continue;
            }

            decimal curAmount, yearAmount;
            try
            {
                curAmount = _formulaEngine.Evaluate(formula.FFormula, curContext);
                yearAmount = _formulaEngine.Evaluate(formula.FFormula, yearContext);
            }
            catch
            {
                curAmount = 0;
                yearAmount = 0;
            }

            curContext.RowResults[formula.FRowIndex] = curAmount;
            yearContext.RowResults[formula.FRowIndex] = yearAmount;

            result.Add(new ProfitStatementDto
            {
                ItemName = formula.FItemName,
                RowIndex = formula.FRowIndex,
                CurrentAmount = curAmount,
                YearAccumulatedAmount = yearAmount
            });
        }

        // 计算营收占比
        var curRevenue = result.FirstOrDefault(r => r.RowIndex == 1)?.CurrentAmount ?? 0;
        var yearRevenue = result.FirstOrDefault(r => r.RowIndex == 1)?.YearAccumulatedAmount ?? 0;
        foreach (var item in result)
        {
            if (curRevenue != 0)
                item.CurrentRevenueRatio = item.CurrentAmount / curRevenue * 100;
            if (yearRevenue != 0)
                item.YearRevenueRatio = item.YearAccumulatedAmount / yearRevenue * 100;
            item.RatioDifference = item.CurrentRevenueRatio - item.YearRevenueRatio;
        }

        return result;
    }

    /// <summary>
    /// 硬编码的简版利润表（Fallback）
    /// </summary>
    private List<ProfitStatementDto> BuildProfitStatementHardcoded(
        Dictionary<string, decimal> currentAmounts,
        Dictionary<string, decimal> yearAmounts)
    {
        decimal GetCurrent(string code) => GetAmountByCodePrefix(currentAmounts, code);
        decimal GetYear(string code) => GetAmountByCodePrefix(yearAmounts, code);

        decimal curRevenue = GetCurrent("5001") + GetCurrent("5051");
        decimal curCost = GetCurrent("5401") + GetCurrent("5402");
        decimal curTax = GetCurrent("5403");
        decimal curSellExp = GetCurrent("5601");
        decimal curMgmtExp = GetCurrent("5602");
        decimal curFinExp = GetCurrent("5603");
        decimal curOtherIncome = GetCurrent("5301");
        decimal curOtherExpense = GetCurrent("5711");
        decimal curIncomeTax = GetCurrent("5801");
        decimal curOperatingProfit = curRevenue - curCost - curTax - curSellExp - curMgmtExp - curFinExp;
        decimal curTotalProfit = curOperatingProfit + curOtherIncome - curOtherExpense;
        decimal curNetProfit = curTotalProfit - curIncomeTax;

        decimal yearRevenue = GetYear("5001") + GetYear("5051");
        decimal yearCost = GetYear("5401") + GetYear("5402");
        decimal yearTax = GetYear("5403");
        decimal yearSellExp = GetYear("5601");
        decimal yearMgmtExp = GetYear("5602");
        decimal yearFinExp = GetYear("5603");
        decimal yearOtherIncome = GetYear("5301");
        decimal yearOtherExpense = GetYear("5711");
        decimal yearIncomeTax = GetYear("5801");
        decimal yearOperatingProfit = yearRevenue - yearCost - yearTax - yearSellExp - yearMgmtExp - yearFinExp;
        decimal yearTotalProfit = yearOperatingProfit + yearOtherIncome - yearOtherExpense;
        decimal yearNetProfit = yearTotalProfit - yearIncomeTax;

        var result = new List<ProfitStatementDto>
        {
            new() { ItemName = "一、营业收入", RowIndex = 1, CurrentAmount = curRevenue, YearAccumulatedAmount = yearRevenue },
            new() { ItemName = "减：营业成本", RowIndex = 2, CurrentAmount = curCost, YearAccumulatedAmount = yearCost },
            new() { ItemName = "营业税金及附加", RowIndex = 3, CurrentAmount = curTax, YearAccumulatedAmount = yearTax },
            new() { ItemName = "销售费用", RowIndex = 4, CurrentAmount = curSellExp, YearAccumulatedAmount = yearSellExp },
            new() { ItemName = "管理费用", RowIndex = 5, CurrentAmount = curMgmtExp, YearAccumulatedAmount = yearMgmtExp },
            new() { ItemName = "财务费用", RowIndex = 6, CurrentAmount = curFinExp, YearAccumulatedAmount = yearFinExp },
            new() { ItemName = "二、营业利润", RowIndex = 7, CurrentAmount = curOperatingProfit, YearAccumulatedAmount = yearOperatingProfit },
            new() { ItemName = "加：营业外收入", RowIndex = 8, CurrentAmount = curOtherIncome, YearAccumulatedAmount = yearOtherIncome },
            new() { ItemName = "减：营业外支出", RowIndex = 9, CurrentAmount = curOtherExpense, YearAccumulatedAmount = yearOtherExpense },
            new() { ItemName = "三、利润总额", RowIndex = 10, CurrentAmount = curTotalProfit, YearAccumulatedAmount = yearTotalProfit },
            new() { ItemName = "减：所得税费用", RowIndex = 11, CurrentAmount = curIncomeTax, YearAccumulatedAmount = yearIncomeTax },
            new() { ItemName = "四、净利润", RowIndex = 12, CurrentAmount = curNetProfit, YearAccumulatedAmount = yearNetProfit }
        };

        foreach (var item in result)
        {
            if (curRevenue != 0)
                item.CurrentRevenueRatio = item.CurrentAmount / curRevenue * 100;
            if (yearRevenue != 0)
                item.YearRevenueRatio = item.YearAccumulatedAmount / yearRevenue * 100;
            item.RatioDifference = item.CurrentRevenueRatio - item.YearRevenueRatio;
        }

        return result;
    }

    public async Task<List<BalanceSheetDto>> GetBalanceSheetAsync(long periodId, long accountSetId = 0)
    {
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();
        var balances = await _balanceRepository.Query()
            .Where(b => b.FPeriodId == periodId && b.FAccountSetId == accountSetId)
            .ToListAsync();

        // 尝试从数据库加载公式
        var formulas = await _formulaRepository.Query()
            .Where(f => f.FReportType == "资产负债表" && f.FAccountSetId == accountSetId && f.FIsEnabled)
            .OrderBy(f => f.FRowIndex)
            .ToListAsync();

        if (formulas.Count > 0)
        {
            return BuildBalanceSheetFromFormulas(formulas, balances, accounts);
        }

        // Fallback: 硬编码逻辑
        return BuildBalanceSheetHardcoded(balances, accounts);
    }

    /// <summary>
    /// 公式驱动的资产负债表构建
    /// </summary>
    private List<BalanceSheetDto> BuildBalanceSheetFromFormulas(
        List<FinReportFormula> formulas,
        List<FinAccountBalance> balances,
        List<FinAccount> accounts)
    {
        // 构建余额上下文（科目编码 → 期末余额、期初余额）
        var endAmounts = new Dictionary<string, decimal>();
        var beginAmounts = new Dictionary<string, decimal>();
        foreach (var account in accounts)
        {
            var bal = balances.Where(b => b.FAccountId == account.FID);
            var endVal = bal.Sum(b => b.FEndDebit - b.FEndCredit);
            var beginVal = bal.Sum(b => b.FBeginDebit - b.FBeginCredit);
            endAmounts[account.FCode] = endVal;
            beginAmounts[account.FCode] = beginVal;
        }

        var endContext = new FormulaContext { AccountAmounts = endAmounts };
        var beginContext = new FormulaContext { AccountAmounts = beginAmounts };
        var result = new List<BalanceSheetDto>();

        foreach (var formula in formulas)
        {
            string category = "资产";
            if (!string.IsNullOrEmpty(formula.FDisplayConfig))
            {
                try
                {
                    var config = JsonSerializer.Deserialize<Dictionary<string, string>>(formula.FDisplayConfig);
                    if (config != null && config.TryGetValue("category", out var cat))
                        category = cat;
                }
                catch { }
            }

            if (string.IsNullOrEmpty(formula.FFormula) || formula.FFormulaType == "header")
            {
                result.Add(new BalanceSheetDto
                {
                    ItemName = formula.FItemName,
                    RowIndex = formula.FRowIndex,
                    LineNo = formula.FAccountCodes,
                    Category = category
                });
                continue;
            }

            decimal endAmount, beginAmount;
            try
            {
                endAmount = _formulaEngine.Evaluate(formula.FFormula, endContext);
                beginAmount = _formulaEngine.Evaluate(formula.FFormula, beginContext);
            }
            catch
            {
                endAmount = 0;
                beginAmount = 0;
            }

            endContext.RowResults[formula.FRowIndex] = endAmount;
            beginContext.RowResults[formula.FRowIndex] = beginAmount;

            result.Add(new BalanceSheetDto
            {
                ItemName = formula.FItemName,
                RowIndex = formula.FRowIndex,
                LineNo = formula.FAccountCodes,
                EndAmount = endAmount,
                BeginAmount = beginAmount,
                Category = category
            });
        }

        return result;
    }

    /// <summary>
    /// 硬编码的资产负债表（Fallback）
    /// </summary>
    private List<BalanceSheetDto> BuildBalanceSheetHardcoded(
        List<FinAccountBalance> balances,
        List<FinAccount> accounts)
    {
        var result = new List<BalanceSheetDto>();

        // 资产
        result.Add(new BalanceSheetDto { ItemName = "流动资产：", RowIndex = 1, Category = "资产" });
        result.Add(CreateBalanceSheetItem("货币资金", 2, "1", balances, accounts, new[] { "1001", "1002", "1012" }));
        result.Add(CreateBalanceSheetItem("应收票据", 3, "2", balances, accounts, new[] { "1121" }));
        result.Add(CreateBalanceSheetItem("应收账款", 4, "3", balances, accounts, new[] { "1122" }));
        result.Add(CreateBalanceSheetItem("预付账款", 5, "4", balances, accounts, new[] { "1123" }));
        result.Add(CreateBalanceSheetItem("其他应收款", 6, "5", balances, accounts, new[] { "1221" }));
        result.Add(CreateBalanceSheetItem("存货", 7, "6", balances, accounts, new[] { "1401", "1403", "1405" }));
        result.Add(new BalanceSheetDto { ItemName = "流动资产合计", RowIndex = 8, LineNo = "7", Category = "资产" });
        
        result.Add(new BalanceSheetDto { ItemName = "非流动资产：", RowIndex = 9, Category = "资产" });
        result.Add(CreateBalanceSheetItem("固定资产原价", 10, "8", balances, accounts, new[] { "1601" }));
        result.Add(CreateBalanceSheetItem("减：累计折旧", 11, "9", balances, accounts, new[] { "1602" }));
        result.Add(CreateBalanceSheetItem("固定资产账面价值", 12, "10", balances, accounts, new[] { "1601" }, new[] { "1602" }));
        result.Add(CreateBalanceSheetItem("无形资产", 13, "11", balances, accounts, new[] { "1701" }));
        result.Add(new BalanceSheetDto { ItemName = "非流动资产合计", RowIndex = 14, LineNo = "12", Category = "资产" });
        result.Add(new BalanceSheetDto { ItemName = "资产总计", RowIndex = 15, LineNo = "13", Category = "资产" });

        // 负债
        result.Add(new BalanceSheetDto { ItemName = "流动负债：", RowIndex = 16, Category = "负债" });
        result.Add(CreateBalanceSheetItem("短期借款", 17, "14", balances, accounts, new[] { "2001" }));
        result.Add(CreateBalanceSheetItem("应付票据", 18, "15", balances, accounts, new[] { "2201" }));
        result.Add(CreateBalanceSheetItem("应付账款", 19, "16", balances, accounts, new[] { "2202" }));
        result.Add(CreateBalanceSheetItem("应付职工薪酬", 20, "17", balances, accounts, new[] { "2211" }));
        result.Add(CreateBalanceSheetItem("应交税费", 21, "18", balances, accounts, new[] { "2221" }));
        result.Add(CreateBalanceSheetItem("其他应付款", 22, "19", balances, accounts, new[] { "2241" }));
        result.Add(new BalanceSheetDto { ItemName = "流动负债合计", RowIndex = 23, LineNo = "20", Category = "负债" });

        // 权益
        result.Add(new BalanceSheetDto { ItemName = "所有者权益：", RowIndex = 24, Category = "权益" });
        result.Add(CreateBalanceSheetItem("实收资本", 25, "21", balances, accounts, new[] { "3001" }));
        result.Add(CreateBalanceSheetItem("盈余公积", 26, "22", balances, accounts, new[] { "3101" }));
        result.Add(CreateBalanceSheetItem("未分配利润", 27, "23", balances, accounts, new[] { "3103", "3104" }));
        result.Add(new BalanceSheetDto { ItemName = "所有者权益合计", RowIndex = 28, LineNo = "24", Category = "权益" });
        result.Add(new BalanceSheetDto { ItemName = "负债和所有者权益总计", RowIndex = 29, LineNo = "25", Category = "权益" });

        return result;
    }

    public async Task<List<CashFlowDto>> GetCashFlowAsync(long startPeriodId, long endPeriodId, long accountSetId = 0)
    {
        var startPeriod = await _periodRepository.Query()
            .FirstOrDefaultAsync(p => p.FID == startPeriodId && p.FAccountSetId == accountSetId);
        var endPeriod = await _periodRepository.Query()
            .FirstOrDefaultAsync(p => p.FID == endPeriodId && p.FAccountSetId == accountSetId);
        if (startPeriod == null || endPeriod == null)
        {
            return new List<CashFlowDto>();
        }

        var formulas = await _formulaRepository.Query()
            .Where(f => f.FReportType == "现金流量表" && f.FAccountSetId == accountSetId && f.FIsEnabled)
            .OrderBy(f => f.FRowIndex)
            .ToListAsync();

        if (formulas.Count > 0)
        {
            return BuildCashFlowFromFormulas(formulas);
        }

        // Fallback: 返回框架结构
        return BuildCashFlowHardcoded();
    }

    /// <summary>
    /// 公式驱动的现金流量表构建
    /// </summary>
    private List<CashFlowDto> BuildCashFlowFromFormulas(List<FinReportFormula> formulas)
    {
        var context = new FormulaContext();
        var result = new List<CashFlowDto>();

        foreach (var formula in formulas)
        {
            string category = "经营";
            if (!string.IsNullOrEmpty(formula.FDisplayConfig))
            {
                try
                {
                    var config = JsonSerializer.Deserialize<Dictionary<string, string>>(formula.FDisplayConfig);
                    if (config != null && config.TryGetValue("category", out var cat))
                        category = cat;
                }
                catch { }
            }

            decimal amount = 0;
            if (!string.IsNullOrEmpty(formula.FFormula) && formula.FFormulaType != "header")
            {
                try { amount = _formulaEngine.Evaluate(formula.FFormula, context); } catch { }
            }

            context.RowResults[formula.FRowIndex] = amount;

            result.Add(new CashFlowDto
            {
                Id = formula.FRowIndex.ToString(),
                ItemName = formula.FItemName,
                RowIndex = formula.FRowIndex,
                Level = formula.FFormulaType == "header" ? 1 : 2,
                IsTotal = formula.FFormulaType == "total",
                CurrentAmount = amount,
                PreviousAmount = 0,
                Amount = amount,
                Category = category
            });
        }

        return result;
    }

    /// <summary>
    /// 硬编码的现金流量表（Fallback）
    /// </summary>
    private static List<CashFlowDto> BuildCashFlowHardcoded()
    {
        var result = new List<CashFlowDto>
        {
            new() { ItemName = "一、经营活动产生的现金流量：", RowIndex = 1, Category = "经营" },
            new() { ItemName = "销售商品、提供劳务收到的现金", RowIndex = 2, Category = "经营" },
            new() { ItemName = "收到的其他与经营活动有关的现金", RowIndex = 3, Category = "经营" },
            new() { ItemName = "现金流入小计", RowIndex = 4, Category = "经营" },
            new() { ItemName = "购买商品、接受劳务支付的现金", RowIndex = 5, Category = "经营" },
            new() { ItemName = "支付给职工以及为职工支付的现金", RowIndex = 6, Category = "经营" },
            new() { ItemName = "支付的各项税费", RowIndex = 7, Category = "经营" },
            new() { ItemName = "支付的其他与经营活动有关的现金", RowIndex = 8, Category = "经营" },
            new() { ItemName = "现金流出小计", RowIndex = 9, Category = "经营" },
            new() { ItemName = "经营活动产生的现金流量净额", RowIndex = 10, Category = "经营" },
            
            new() { ItemName = "二、投资活动产生的现金流量：", RowIndex = 11, Category = "投资" },
            new() { ItemName = "投资活动产生的现金流量净额", RowIndex = 12, Category = "投资" },
            
            new() { ItemName = "三、筹资活动产生的现金流量：", RowIndex = 13, Category = "筹资" },
            new() { ItemName = "筹资活动产生的现金流量净额", RowIndex = 14, Category = "筹资" },
            
            new() { ItemName = "四、现金及现金等价物净增加额", RowIndex = 15, Category = "经营" }
        };

        foreach (var row in result)
        {
            row.Id = row.RowIndex.ToString();
            row.Level = row.ItemName.Contains("、") ? 1 : 2;
            row.IsTotal = row.ItemName.Contains("小计") || row.ItemName.Contains("净额") || row.ItemName.Contains("净增加额");
            row.CurrentAmount = row.Amount;
            row.PreviousAmount = 0;
        }

        return result;
    }

    public async Task<List<TaxPayableDto>> GetTaxPayableAsync(long periodId, long accountSetId = 0)
    {
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();
        var balances = await _balanceRepository.Query()
            .Where(b => b.FPeriodId == periodId && b.FAccountSetId == accountSetId)
            .ToListAsync();

        var taxAccounts = accounts.Where(a => a.FCode.StartsWith("2221")).ToList();

        return taxAccounts.Select(a =>
        {
            var balance = balances.FirstOrDefault(b => b.FAccountId == a.FID);
            return new TaxPayableDto
            {
                TaxName = a.FName,
                OpeningBalance = balance?.FBeginCredit ?? 0,
                PeriodIncrease = balance?.FCurrentCredit ?? 0,
                PeriodDecrease = balance?.FCurrentDebit ?? 0,
                ClosingBalance = balance?.FEndCredit ?? 0,
                BeginAmount = balance?.FBeginCredit ?? 0,
                CurrentPayable = balance?.FCurrentCredit ?? 0,
                CurrentPaid = balance?.FCurrentDebit ?? 0,
                EndAmount = balance?.FEndCredit ?? 0
            };
        }).ToList();
    }

    public async Task<AmoebaPLReportDto> GetAmoebaPLAsync(long startPeriodId, long endPeriodId, long? departmentId = null, long? amoebaId = null, long accountSetId = 0)
    {
        var template = await _templateRepository.Query()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.FIsDefault == 1);

        if (template == null)
        {
            return new AmoebaPLReportDto();
        }

        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();
        var balances = await _balanceRepository.Query()
            .Where(b => b.FPeriodId >= startPeriodId && b.FPeriodId <= endPeriodId && b.FAccountSetId == accountSetId)
            .ToListAsync();

        var summaryTable = BuildAmoebaPLSummary(template.Items, accounts, balances);

        // 计算图表数据：顶级 group 节点按名称匹配
        var totalIncome = summaryTable
            .Where(i => i.NodeRole == "group" && i.ItemName.Contains("收入"))
            .Sum(i => i.CurrentAmount);
        
        var totalExpense = summaryTable
            .Where(i => i.NodeRole == "group" && i.ItemName.Contains("支出"))
            .Sum(i => i.CurrentAmount);
        
        var profit = totalIncome - totalExpense;

        var chartData = new AmoebaPLChartDto
        {
            PieChartData = new[]
            {
                new ChartDataItem { Name = "收入", Value = totalIncome },
                new ChartDataItem { Name = "支出", Value = totalExpense },
                new ChartDataItem { Name = "损益", Value = profit }
            },
            TrendData = Enumerable.Range(1, 12).Select(m => new TrendDataItem
            {
                Month = m,
                Income = totalIncome / 12,
                Expense = totalExpense / 12,
                Profit = profit / 12
            }).ToArray(),
            CompareData = new[]
            {
                new CompareDataItem { Name = "收入", CurrentValue = totalIncome, PreviousValue = totalIncome * 0.9m },
                new CompareDataItem { Name = "支出", CurrentValue = totalExpense, PreviousValue = totalExpense * 0.95m },
                new CompareDataItem { Name = "损益", CurrentValue = profit, PreviousValue = profit * 1.1m }
            }
        };

        return new AmoebaPLReportDto
        {
            ChartData = chartData,
            SummaryTable = summaryTable
        };
    }

    public async Task<List<SmallEnterpriseProfitStatementDto>> GetSmallEnterpriseProfitStatementAsync(int year, int month, long accountSetId = 0)
    {
        // 获取科目列表
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();
        
        // 计算本期日期范围
        var currentStartDate = new DateTime(year, month, 1);
        var currentEndDate = currentStartDate.AddMonths(1).AddDays(-1);
        
        // 计算本年累计日期范围（从1月到当前月份）
        var yearStartDate = new DateTime(year, 1, 1);
        var yearEndDate = currentEndDate;
        
        // 获取本期已过账凭证分录
        var currentEntries = await (from e in _voucherEntryRepository.Query()
                                     join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                                     where (v.FStatus == 2 || v.FStatus == 3) && v.FSource != VoucherSource.SystemClosing && v.FDate >= currentStartDate && v.FDate <= currentEndDate
                                     select e).ToListAsync();
        
        // 获取本年累计已过账凭证分录
        var yearEntries = await (from e in _voucherEntryRepository.Query()
                                 join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                                 where (v.FStatus == 2 || v.FStatus == 3) && v.FSource != VoucherSource.SystemClosing && v.FDate >= yearStartDate && v.FDate <= yearEndDate
                                 select e).ToListAsync();
        
        // 计算各科目金额
        var currentAmounts = CalculateAccountAmounts(currentEntries, accounts);
        var yearAmounts = CalculateAccountAmounts(yearEntries, accounts);
        
        // 尝试从数据库加载公式
        var formulas = await _formulaRepository.Query()
            .Where(f => f.FReportType == "小企业利润表" && f.FAccountSetId == accountSetId && f.FIsEnabled)
            .OrderBy(f => f.FRowIndex)
            .ToListAsync();

        if (formulas.Count > 0)
        {
            return BuildSmallEnterpriseProfitFromFormulas(formulas, currentAmounts, yearAmounts);
        }

        // Fallback: 硬编码逻辑
        return BuildSmallEnterpriseProfitStatement(currentAmounts, yearAmounts);
    }

    /// <summary>
    /// 公式驱动的小企业利润表构建
    /// </summary>
    private List<SmallEnterpriseProfitStatementDto> BuildSmallEnterpriseProfitFromFormulas(
        List<FinReportFormula> formulas,
        Dictionary<string, decimal> currentAmounts,
        Dictionary<string, decimal> yearAmounts)
    {
        var curContext = new FormulaContext { AccountAmounts = currentAmounts };
        var yearContext = new FormulaContext { AccountAmounts = yearAmounts };
        var result = new List<SmallEnterpriseProfitStatementDto>();

        foreach (var formula in formulas)
        {
            // 解析显示配置
            bool isMainTitle = false, isSubTitle = false, isIndent = false;
            int indentLevel = 0;
            if (!string.IsNullOrEmpty(formula.FDisplayConfig))
            {
                try
                {
                    var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(formula.FDisplayConfig);
                    if (config != null)
                    {
                        if (config.TryGetValue("isMainTitle", out var mt)) isMainTitle = mt.GetBoolean();
                        if (config.TryGetValue("isSubTitle", out var st)) isSubTitle = st.GetBoolean();
                        if (config.TryGetValue("isIndent", out var ind)) isIndent = ind.GetBoolean();
                        if (config.TryGetValue("indentLevel", out var il)) indentLevel = il.GetInt32();
                    }
                }
                catch { }
            }

            decimal curAmount = 0, yearAmount = 0;

            if (!string.IsNullOrEmpty(formula.FFormula) && formula.FFormulaType != "header")
            {
                try
                {
                    curAmount = _formulaEngine.Evaluate(formula.FFormula, curContext);
                    yearAmount = _formulaEngine.Evaluate(formula.FFormula, yearContext);
                }
                catch { }
            }

            curContext.RowResults[formula.FRowIndex] = curAmount;
            yearContext.RowResults[formula.FRowIndex] = yearAmount;

            result.Add(new SmallEnterpriseProfitStatementDto
            {
                ItemName = formula.FItemName,
                RowIndex = formula.FRowIndex,
                CurrentAmount = curAmount,
                YearAccumulatedAmount = yearAmount,
                IsMainTitle = isMainTitle,
                IsSubTitle = isSubTitle,
                IsIndent = isIndent,
                IndentLevel = indentLevel
            });
        }

        return result;
    }
    
    /// <summary>
    /// 计算各科目金额（返回科目编码到金额的映射）
    /// </summary>
    private Dictionary<string, decimal> CalculateAccountAmounts(List<FinVoucherEntry> entries, List<FinAccount> accounts)
    {
        var result = new Dictionary<string, decimal>();
        
        // 只处理叶子科目，使用精确匹配，避免父子科目双重计算
        foreach (var account in accounts.Where(a => a.FIsLeaf == 1))
        {
            var code = account.FCode;
            var accountEntries = entries.Where(e => e.FAccountCode == code).ToList();
            
            if (!accountEntries.Any()) continue;
            
            decimal amount;
            if (FinAccountCategory.IsProfitLoss(account.FCategory))
            {
                // 损益类科目
                if (account.FBalanceDirection == "贷")
                {
                    // 收入类：贷方 - 借方
                    amount = accountEntries.Sum(e => e.FCreditAmount) - accountEntries.Sum(e => e.FDebitAmount);
                }
                else
                {
                    // 费用类：借方 - 贷方
                    amount = accountEntries.Sum(e => e.FDebitAmount) - accountEntries.Sum(e => e.FCreditAmount);
                }
            }
            else
            {
                amount = 0;
            }
            
            result[code] = amount;
        }
        
        return result;
    }
    
    /// <summary>
    /// 获取科目金额（支持科目编码前缀匹配）
    /// </summary>
    private decimal GetAmountByCodePrefix(Dictionary<string, decimal> amounts, string codePrefix)
    {
        return amounts
            .Where(kv => kv.Key.StartsWith(codePrefix))
            .Sum(kv => kv.Value);
    }
    
    /// <summary>
    /// 构建小企业利润表34行数据
    /// </summary>
    private List<SmallEnterpriseProfitStatementDto> BuildSmallEnterpriseProfitStatement(
        Dictionary<string, decimal> currentAmounts, 
        Dictionary<string, decimal> yearAmounts)
    {
        // 辅助函数：获取科目金额
        decimal GetCurrent(string code) => GetAmountByCodePrefix(currentAmounts, code);
        decimal GetYear(string code) => GetAmountByCodePrefix(yearAmounts, code);
        
        // 按规则计算各项目金额
        // 行1：营业收入 = 5001 + 5051
        var row1_Current = GetCurrent("5001") + GetCurrent("5051");
        var row1_Year = GetYear("5001") + GetYear("5051");
        
        // 行2：营业成本 = 5401 + 5402
        var row2_Current = GetCurrent("5401") + GetCurrent("5402");
        var row2_Year = GetYear("5401") + GetYear("5402");
        
        // 行3：营业税金及附加 = 5403
        var row3_Current = GetCurrent("5403");
        var row3_Year = GetYear("5403");
        
        // 行4-10：营业税金及附加明细
        var row4_Current = GetCurrent("540301"); // 消费税
        var row4_Year = GetYear("540301");
        var row5_Current = GetCurrent("540302"); // 营业税（实际上可能是城建税）
        var row5_Year = GetYear("540302");
        var row6_Current = GetCurrent("540302"); // 城市维护建设税（同营业税科目）
        var row6_Year = GetYear("540302");
        var row7_Current = GetCurrent("540310"); // 资源税
        var row7_Year = GetYear("540310");
        var row8_Current = GetCurrent("540305"); // 土地增值税
        var row8_Year = GetYear("540305");
        // 行9：城镇土地使用税、房产税、车船税、印花税
        var row9_Current = GetCurrent("540306") + GetCurrent("540307") + GetCurrent("540308") + GetCurrent("540309");
        var row9_Year = GetYear("540306") + GetYear("540307") + GetYear("540308") + GetYear("540309");
        // 行10：教育费附加、矿产资源补偿费、排污费
        var row10_Current = GetCurrent("540303") + GetCurrent("540304") + GetCurrent("540311") + GetCurrent("540312");
        var row10_Year = GetYear("540303") + GetYear("540304") + GetYear("540311") + GetYear("540312");
        
        // 行11：销售费用 = 5601
        var row11_Current = GetCurrent("5601");
        var row11_Year = GetYear("5601");
        
        // 行12-13：销售费用明细
        var row12_Current = GetCurrent("560107"); // 商品维修费
        var row12_Year = GetYear("560107");
        var row13_Current = GetCurrent("560115"); // 广告费和业务宣传费
        var row13_Year = GetYear("560115");
        
        // 行14：管理费用 = 5602
        var row14_Current = GetCurrent("5602");
        var row14_Year = GetYear("5602");
        
        // 行15-17：管理费用明细
        var row15_Current = GetCurrent("560221"); // 开办费
        var row15_Year = GetYear("560221");
        var row16_Current = GetCurrent("560214"); // 业务招待费
        var row16_Year = GetYear("560214");
        var row17_Current = GetCurrent("560222"); // 研究费用
        var row17_Year = GetYear("560222");
        
        // 行18：财务费用 = 5603
        var row18_Current = GetCurrent("5603");
        var row18_Year = GetYear("5603");
        
        // 行19：利息费用 = 560301
        var row19_Current = GetCurrent("560301");
        var row19_Year = GetYear("560301");
        
        // 行20：其他损失（暂无对应科目）
        var row20_Current = 0m;
        var row20_Year = 0m;
        
        // 行21：投资收益 = 5111
        var row21_Current = GetCurrent("5111");
        var row21_Year = GetYear("5111");
        
        // 行22：其他收益（暂无对应科目）
        var row22_Current = 0m;
        var row22_Year = 0m;
        
        // 行23：营业利润 = 行1-行2-行3-行11-行14-行18-行20+行21+行22
        var row23_Current = row1_Current - row2_Current - row3_Current - row11_Current - row14_Current - row18_Current - row20_Current + row21_Current + row22_Current;
        var row23_Year = row1_Year - row2_Year - row3_Year - row11_Year - row14_Year - row18_Year - row20_Year + row21_Year + row22_Year;
        
        // 行24：营业外收入 = 5301
        var row24_Current = GetCurrent("5301");
        var row24_Year = GetYear("5301");
        
        // 行25：政府补助 = 530107
        var row25_Current = GetCurrent("530107");
        var row25_Year = GetYear("530107");
        
        // 行26：营业外支出 = 5711
        var row26_Current = GetCurrent("5711");
        var row26_Year = GetYear("5711");
        
        // 行27-31：营业外支出明细
        var row27_Current = GetCurrent("571103"); // 坏账损失
        var row27_Year = GetYear("571103");
        var row28_Current = GetCurrent("571104"); // 无法收回的长期债券投资损失
        var row28_Year = GetYear("571104");
        var row29_Current = GetCurrent("571105"); // 无法收回的长期股权投资损失
        var row29_Year = GetYear("571105");
        var row30_Current = GetCurrent("571106"); // 自然灾害等不可抗力因素造成的损失
        var row30_Year = GetYear("571106");
        var row31_Current = GetCurrent("571107"); // 税收滞纳金
        var row31_Year = GetYear("571107");
        
        // 行32：利润总额 = 行23 + 行24 - 行26
        var row32_Current = row23_Current + row24_Current - row26_Current;
        var row32_Year = row23_Year + row24_Year - row26_Year;
        
        // 行33：所得税费用 = 5801
        var row33_Current = GetCurrent("5801");
        var row33_Year = GetYear("5801");
        
        // 行34：净利润 = 行32 - 行33
        var row34_Current = row32_Current - row33_Current;
        var row34_Year = row32_Year - row33_Year;
        
        // 构建返回结果
        return new List<SmallEnterpriseProfitStatementDto>
        {
            new() { ItemName = "一、营业收入", RowIndex = 1, YearAccumulatedAmount = row1_Year, CurrentAmount = row1_Current, IsMainTitle = true, IndentLevel = 0 },
            new() { ItemName = "减：营业成本", RowIndex = 2, YearAccumulatedAmount = row2_Year, CurrentAmount = row2_Current, IsSubTitle = true, IndentLevel = 0 },
            new() { ItemName = "营业税金及附加", RowIndex = 3, YearAccumulatedAmount = row3_Year, CurrentAmount = row3_Current, IsSubTitle = true, IndentLevel = 0 },
            new() { ItemName = "其中：消费税", RowIndex = 4, YearAccumulatedAmount = row4_Year, CurrentAmount = row4_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "营业税", RowIndex = 5, YearAccumulatedAmount = row5_Year, CurrentAmount = row5_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "城市维护建设税", RowIndex = 6, YearAccumulatedAmount = row6_Year, CurrentAmount = row6_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "资源税", RowIndex = 7, YearAccumulatedAmount = row7_Year, CurrentAmount = row7_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "土地增值税", RowIndex = 8, YearAccumulatedAmount = row8_Year, CurrentAmount = row8_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "城镇土地使用税、房产税、车船税、印花税", RowIndex = 9, YearAccumulatedAmount = row9_Year, CurrentAmount = row9_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "教育费附加、矿产资源补偿费、排污费", RowIndex = 10, YearAccumulatedAmount = row10_Year, CurrentAmount = row10_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "销售费用", RowIndex = 11, YearAccumulatedAmount = row11_Year, CurrentAmount = row11_Current, IsSubTitle = true, IndentLevel = 0 },
            new() { ItemName = "其中：商品维修费", RowIndex = 12, YearAccumulatedAmount = row12_Year, CurrentAmount = row12_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "广告费和业务宣传费", RowIndex = 13, YearAccumulatedAmount = row13_Year, CurrentAmount = row13_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "管理费用", RowIndex = 14, YearAccumulatedAmount = row14_Year, CurrentAmount = row14_Current, IsSubTitle = true, IndentLevel = 0 },
            new() { ItemName = "其中：开办费", RowIndex = 15, YearAccumulatedAmount = row15_Year, CurrentAmount = row15_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "业务招待费", RowIndex = 16, YearAccumulatedAmount = row16_Year, CurrentAmount = row16_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "研究费用", RowIndex = 17, YearAccumulatedAmount = row17_Year, CurrentAmount = row17_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "财务费用", RowIndex = 18, YearAccumulatedAmount = row18_Year, CurrentAmount = row18_Current, IsSubTitle = true, IndentLevel = 0 },
            new() { ItemName = "其中：利息费用", RowIndex = 19, YearAccumulatedAmount = row19_Year, CurrentAmount = row19_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "其他损失", RowIndex = 20, YearAccumulatedAmount = row20_Year, CurrentAmount = row20_Current, IsSubTitle = true, IndentLevel = 0 },
            new() { ItemName = "加：投资收益", RowIndex = 21, YearAccumulatedAmount = row21_Year, CurrentAmount = row21_Current, IsSubTitle = true, IndentLevel = 0 },
            new() { ItemName = "其他收益", RowIndex = 22, YearAccumulatedAmount = row22_Year, CurrentAmount = row22_Current, IsIndent = true, IndentLevel = 0 },
            new() { ItemName = "二、营业利润", RowIndex = 23, YearAccumulatedAmount = row23_Year, CurrentAmount = row23_Current, IsMainTitle = true, IndentLevel = 0 },
            new() { ItemName = "加：营业外收入", RowIndex = 24, YearAccumulatedAmount = row24_Year, CurrentAmount = row24_Current, IsSubTitle = true, IndentLevel = 0 },
            new() { ItemName = "其中：政府补助", RowIndex = 25, YearAccumulatedAmount = row25_Year, CurrentAmount = row25_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "减：营业外支出", RowIndex = 26, YearAccumulatedAmount = row26_Year, CurrentAmount = row26_Current, IsSubTitle = true, IndentLevel = 0 },
            new() { ItemName = "其中：坏账损失", RowIndex = 27, YearAccumulatedAmount = row27_Year, CurrentAmount = row27_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "无法收回的长期债券投资损失", RowIndex = 28, YearAccumulatedAmount = row28_Year, CurrentAmount = row28_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "无法收回的长期股权投资损失", RowIndex = 29, YearAccumulatedAmount = row29_Year, CurrentAmount = row29_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "自然灾害等不可抗力因素造成的损失", RowIndex = 30, YearAccumulatedAmount = row30_Year, CurrentAmount = row30_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "税收滞纳金", RowIndex = 31, YearAccumulatedAmount = row31_Year, CurrentAmount = row31_Current, IsIndent = true, IndentLevel = 1 },
            new() { ItemName = "三、利润总额", RowIndex = 32, YearAccumulatedAmount = row32_Year, CurrentAmount = row32_Current, IsMainTitle = true, IndentLevel = 0 },
            new() { ItemName = "减：所得税费用", RowIndex = 33, YearAccumulatedAmount = row33_Year, CurrentAmount = row33_Current, IsSubTitle = true, IndentLevel = 0 },
            new() { ItemName = "四、净利润", RowIndex = 34, YearAccumulatedAmount = row34_Year, CurrentAmount = row34_Current, IsMainTitle = true, IndentLevel = 0 }
        };
    }
    
    public async Task<bool> RecalculateBalanceAsync(long periodId, long accountSetId = 0)
    {
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();
        
        // 获取上期余额作为本期期初
        var currentPeriod = await _periodRepository.GetByIdAsync(periodId);
        if (currentPeriod == null) return false;

        // 上期跨年回退：1月(FPeriodNo==1)的上期是上一年12月，否则同年上一期。
        // 否则1月重算取不到上期 → 期初全部按0覆盖，抹掉年结转来的期初余额（现金/应收/权益等历年累计）。
        var prevPeriod = currentPeriod.FPeriodNo > 1
            ? await _periodRepository.Query()
                .Where(p => p.FYear == currentPeriod.FYear && p.FPeriodNo == currentPeriod.FPeriodNo - 1 && p.FAccountSetId == accountSetId)
                .FirstOrDefaultAsync()
            : await _periodRepository.Query()
                .Where(p => p.FYear == currentPeriod.FYear - 1 && p.FPeriodNo == 12 && p.FAccountSetId == accountSetId)
                .FirstOrDefaultAsync();

        var prevBalances = prevPeriod != null 
            ? await _balanceRepository.Query().Where(b => b.FPeriodId == prevPeriod.FID && b.FAccountSetId == accountSetId).ToListAsync()
            : new List<FinAccountBalance>();

        // 从凭证分录汇总本期发生额
        var entries = await (from e in _voucherEntryRepository.Query()
                             join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                             // 含已锁定(结账后 FStatus=3)：否则结账后重算把已入账凭证当空集，本期发生额清零、余额被销毁
                             where v.FPeriodId == periodId && (v.FStatus == 2 || v.FStatus == 3)
                             select e).ToListAsync();

        foreach (var account in accounts.Where(a => a.FIsLeaf == 1))
        {
            var prevBalance = prevBalances.FirstOrDefault(b => b.FAccountId == account.FID);
            var accountEntries = entries.Where(e => e.FAccountId == account.FID);

            var currentDebit = accountEntries.Sum(e => e.FDebitAmount);
            var currentCredit = accountEntries.Sum(e => e.FCreditAmount);

            var beginDebit = prevBalance?.FEndDebit ?? 0;
            var beginCredit = prevBalance?.FEndCredit ?? 0;

            // 计算期末余额
            decimal endDebit, endCredit;
            if (account.FBalanceDirection == "借")
            {
                var balance = beginDebit - beginCredit + currentDebit - currentCredit;
                endDebit = balance > 0 ? balance : 0;
                endCredit = balance < 0 ? -balance : 0;
            }
            else
            {
                var balance = beginCredit - beginDebit + currentCredit - currentDebit;
                endDebit = balance < 0 ? -balance : 0;
                endCredit = balance > 0 ? balance : 0;
            }

            var existing = await _balanceRepository.Query()
                .FirstOrDefaultAsync(b => b.FPeriodId == periodId && b.FAccountId == account.FID);

            if (existing != null)
            {
                existing.FBeginDebit = beginDebit;
                existing.FBeginCredit = beginCredit;
                existing.FCurrentDebit = currentDebit;
                existing.FCurrentCredit = currentCredit;
                existing.FEndDebit = endDebit;
                existing.FEndCredit = endCredit;
                existing.FAccountSetId = accountSetId; // 自愈历史 FAccountSetId=0 脏行，保证报表/试算可见
                existing.FUpdatedTime = DateTime.Now;
                await _balanceRepository.UpdateAsync(existing);
            }
            else
            {
                var balance = new FinAccountBalance
                {
                    FPeriodId = periodId,
                    FAccountId = account.FID,
                    FAccountSetId = accountSetId, // 必须显式设置账套号，否则默认 0 会对所有按账套过滤的报表/试算不可见
                    FBeginDebit = beginDebit,
                    FBeginCredit = beginCredit,
                    FCurrentDebit = currentDebit,
                    FCurrentCredit = currentCredit,
                    FEndDebit = endDebit,
                    FEndCredit = endCredit,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };
                await _balanceRepository.AddAsync(balance);
            }
        }

        return true;
    }

    #region Helper Methods

    private decimal GetAccountAmount(List<FinAccountBalance> balances, List<FinAccount> accounts, string accountCodePrefix)
    {
        // 使用前缀匹配，支持匹配父级和子级科目
        var matchedAccounts = accounts.Where(a => a.FCode.StartsWith(accountCodePrefix)).ToList();
        if (!matchedAccounts.Any()) return 0;

        var accountIds = matchedAccounts.Select(a => a.FID).ToHashSet();
        var matchedBalances = balances.Where(b => accountIds.Contains(b.FAccountId));

        // 根据科目余额方向计算金额
        // 收入类（贷方余额）：贷方 - 借方 → 正值
        // 费用类（借方余额）：借方 - 贷方 → 正值
        var direction = matchedAccounts.First().FBalanceDirection;
        if (direction == "贷")
        {
            return matchedBalances.Sum(b => b.FCurrentCredit - b.FCurrentDebit);
        }
        else
        {
            return matchedBalances.Sum(b => b.FCurrentDebit - b.FCurrentCredit);
        }
    }

    private BalanceSheetDto CreateBalanceSheetItem(string name, int rowIndex, string lineNo, 
        List<FinAccountBalance> balances, List<FinAccount> accounts, string[] accountCodes, string[]? minusCodes = null)
    {
        var accountIds = accounts
            .Where(a => accountCodes.Any(c => a.FCode.StartsWith(c)))
            .Select(a => a.FID)
            .ToList();

        var endAmount = balances
            .Where(b => accountIds.Contains(b.FAccountId))
            .Sum(b => b.FEndDebit - b.FEndCredit);

        var beginAmount = balances
            .Where(b => accountIds.Contains(b.FAccountId))
            .Sum(b => b.FBeginDebit - b.FBeginCredit);

        if (minusCodes != null)
        {
            var minusIds = accounts
                .Where(a => minusCodes.Any(c => a.FCode.StartsWith(c)))
                .Select(a => a.FID)
                .ToList();

            endAmount -= balances
                .Where(b => minusIds.Contains(b.FAccountId))
                .Sum(b => b.FEndDebit - b.FEndCredit);

            beginAmount -= balances
                .Where(b => minusIds.Contains(b.FAccountId))
                .Sum(b => b.FBeginDebit - b.FBeginCredit);
        }

        return new BalanceSheetDto
        {
            ItemName = name,
            RowIndex = rowIndex,
            LineNo = lineNo,
            EndAmount = endAmount,
            BeginAmount = beginAmount,
            Category = "资产"
        };
    }

    private List<AmoebaPLSummaryDto> BuildAmoebaPLSummary(List<FinAmoebaPLItem> items, List<FinAccount> accounts, List<FinAccountBalance> balances)
    {
        var lookup = items.ToLookup(i => i.FParentId);

        AmoebaPLSummaryDto BuildNode(FinAmoebaPLItem item)
        {
            var relatedAccounts = new List<long>();
            if (!string.IsNullOrEmpty(item.FRelatedAccountsJson))
            {
                try
                {
                    var codes = JsonSerializer.Deserialize<List<string>>(item.FRelatedAccountsJson);
                    if (codes != null)
                    {
                        relatedAccounts = accounts
                            .Where(a => codes.Any(c => a.FCode.StartsWith(c)))
                            .Select(a => a.FID)
                            .ToList();
                    }
                }
                catch { }
            }

            var currentAmount = balances
                .Where(b => relatedAccounts.Contains(b.FAccountId))
                .Sum(b => b.FCurrentCredit - b.FCurrentDebit);

            var yearAmount = balances
                .Where(b => relatedAccounts.Contains(b.FAccountId))
                .Sum(b => b.FCurrentCredit - b.FCurrentDebit);

            var node = new AmoebaPLSummaryDto
            {
                ItemId = item.FID,
                ItemName = item.FItemName,
                NodeRole = item.FNodeRole,
                Depth = 0,
                CurrentAmount = currentAmount,
                YearAccumulatedAmount = yearAmount
            };

            node.Children = lookup[item.FID].Select(BuildNode).OrderBy(i => i.ItemId).ToList();
            return node;
        }

        var result = lookup[0].Select(BuildNode).OrderBy(i => i.ItemId).ToList();

        // 计算营收占比
        var totalRevenue = result.Where(i => i.NodeRole == "group" && i.ItemName.Contains("收入")).Sum(i => i.CurrentAmount);
        if (totalRevenue != 0)
        {
            foreach (var item in result)
            {
                CalculateRatios(item, totalRevenue);
            }
        }

        return result;
    }

    private void CalculateRatios(AmoebaPLSummaryDto item, decimal totalRevenue)
    {
        item.CurrentRevenueRatio = item.CurrentAmount / totalRevenue * 100;
        item.YearRevenueRatio = item.YearAccumulatedAmount / totalRevenue * 100;
        item.RatioDifference = item.CurrentRevenueRatio - item.YearRevenueRatio;

        foreach (var child in item.Children)
        {
            CalculateRatios(child, totalRevenue);
        }
    }

    #endregion

    /// <summary>
    /// 科目明细账查询：返回某科目在指定期间的期初余额 + 凭证分录 + 逐笔余额
    /// </summary>
    public async Task<AccountDetailResultDto> GetAccountDetailAsync(long accountId, int year, int periodNo, long accountSetId = 0)
    {
        var account = await _accountRepository.Query().FirstOrDefaultAsync(a => a.FID == accountId);
        if (account == null)
        {
            return new AccountDetailResultDto();
        }

        // 获取指定期间及之前所有凟出凭证分录
        var allEntries = await (from e in _voucherEntryRepository.Query()
                                 join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                                 join p in _periodRepository.Query().Where(p => p.FAccountSetId == accountSetId) on v.FPeriodId equals p.FID
                                 where e.FAccountId == accountId && (v.FStatus == 2 || v.FStatus == 3) && p.FYear == year && p.FPeriodNo <= periodNo
                                 orderby p.FPeriodNo, v.FDate, e.FID
                                 select new
                                 {
                                     e.FID,
                                     e.FDebitAmount,
                                     e.FCreditAmount,
                                     e.FSummary,
                                     p.FPeriodNo,
                                     v.FDate,
                                     VoucherWord = v.FVoucherWord,
                                     VoucherNo = v.FVoucherNo
                                 }).ToListAsync();

        // 期初余额：当期之前累计
        var beginDebit = allEntries.Where(x => x.FPeriodNo < periodNo).Sum(x => x.FDebitAmount);
        var beginCredit = allEntries.Where(x => x.FPeriodNo < periodNo).Sum(x => x.FCreditAmount);
        var (beginDebitBal, beginCreditBal) = CalculateBalance(beginDebit, beginCredit, account.FBalanceDirection);
        decimal runningBalance = beginDebitBal - beginCreditBal;

        var items = new List<AccountDetailDto>();

        // 添加期初行
        var directionLabel = beginDebitBal >= beginCreditBal ? "借" : "贷";
        items.Add(new AccountDetailDto
        {
            Date = $"{year}年第{String.Format("{0:00}", periodNo)}期期初",
            VoucherNo = "",
            Summary = "期初余额",
            DebitAmount = beginDebitBal > 0 ? beginDebitBal : 0,
            CreditAmount = beginCreditBal > 0 ? beginCreditBal : 0,
            Balance = Math.Abs(beginDebitBal - beginCreditBal),
            Direction = directionLabel,
            IsOpeningBalance = true
        });

        // 本期函证分录
        var currentEntries = allEntries.Where(x => x.FPeriodNo == periodNo).ToList();
        foreach (var entry in currentEntries)
        {
            runningBalance += (decimal)(entry.FDebitAmount - entry.FCreditAmount);
            var balDir = runningBalance >= 0 ? "借" : "贷";
            items.Add(new AccountDetailDto
            {
                Date = entry.FDate.ToString("yyyy-MM-dd"),
                VoucherNo = $"{entry.VoucherWord}{entry.VoucherNo}",
                Summary = entry.FSummary ?? "",
                DebitAmount = entry.FDebitAmount,
                CreditAmount = entry.FCreditAmount,
                Balance = Math.Abs(runningBalance),
                Direction = balDir,
                IsOpeningBalance = false
            });
        }

        return new AccountDetailResultDto
        {
            AccountCode = account.FCode,
            AccountName = account.FName,
            Items = items
        };
    }

    /// <summary>
    /// 钻取明细：根据报表类型和行索引查询关联科目的凭证分录
    /// </summary>
    public async Task<List<DrillDownItemDto>> GetDrillDownAsync(string reportType, int rowIndex, int year, int month, long accountSetId, string? accountCode = null)
    {
        var accountCodes = new List<string>();

        // 如果直接传入了科目编码，优先使用
        if (!string.IsNullOrEmpty(accountCode))
        {
            accountCodes.Add(accountCode);
        }
        else
        {
            // 查找对应的公式配置
            var formula = await _formulaRepository.Query()
                .FirstOrDefaultAsync(f => f.FReportType == reportType && f.FRowIndex == rowIndex && f.FAccountSetId == accountSetId && f.FIsEnabled);

            if (formula == null || string.IsNullOrEmpty(formula.FFormula))
                return new List<DrillDownItemDto>();

            // 从公式解析科目编码
            if (!string.IsNullOrEmpty(formula.FAccountCodes))
            {
                accountCodes = formula.FAccountCodes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            }
            else
            {
                var matches = Regex.Matches(formula.FFormula, @"ACCOUNT\(([^)]+)\)", RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    accountCodes.Add(match.Groups[1].Value.Trim());
                }
            }
        }

        if (accountCodes.Count == 0)
            return new List<DrillDownItemDto>();

        // 获取匹配的科目
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();
        var matchedAccountIds = accounts
            .Where(a => accountCodes.Any(c => a.FCode.StartsWith(c)))
            .Select(a => a.FID)
            .ToHashSet();

        // 查询指定期间内这些科目的已审核凭证分录
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var entries = await (from e in _voucherEntryRepository.Query()
                             join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                             where (v.FStatus == 2 || v.FStatus == 3) && v.FDate >= startDate && v.FDate <= endDate
                                   && matchedAccountIds.Contains(e.FAccountId)
                             orderby v.FDate, v.FVoucherNo
                             select new DrillDownItemDto
                             {
                                 VoucherId = v.FID,
                                 VoucherNo = v.FVoucherWord + v.FVoucherNo.ToString(),
                                 VoucherDate = v.FDate,
                                 Summary = e.FSummary ?? "",
                                 DebitAmount = e.FDebitAmount,
                                 CreditAmount = e.FCreditAmount,
                                 AccountCode = e.FAccountCode ?? "",
                                 AccountName = ""
                             }).ToListAsync();

        // 填充科目名称
        var accountDict = accounts.ToDictionary(a => a.FCode, a => a.FName);
        foreach (var entry in entries)
        {
            if (accountDict.TryGetValue(entry.AccountCode, out var name))
                entry.AccountName = name;
        }

        return entries;
    }

    /// <summary>
    /// 利润趋势：遁1-12月计算收入/成本/费用/净利润
    /// </summary>
    public async Task<List<ProfitTrendDto>> GetProfitTrendAsync(int year, long accountSetId)
    {
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();

        var result = new List<ProfitTrendDto>();

        for (int month = 1; month <= 12; month++)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var entries = await (from e in _voucherEntryRepository.Query()
                                 join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                                 where (v.FStatus == 2 || v.FStatus == 3) && v.FSource != VoucherSource.SystemClosing && v.FDate >= startDate && v.FDate <= endDate
                                 select e).ToListAsync();

            var amounts = CalculateAccountAmounts(entries, accounts);

            var revenue = GetAmountByCodePrefix(amounts, "5001") + GetAmountByCodePrefix(amounts, "5051");
            var cost = GetAmountByCodePrefix(amounts, "5401") + GetAmountByCodePrefix(amounts, "5402");
            var expense = GetAmountByCodePrefix(amounts, "5601") + GetAmountByCodePrefix(amounts, "5602") + GetAmountByCodePrefix(amounts, "5603");
            var profit = revenue - cost - expense;

            result.Add(new ProfitTrendDto
            {
                Month = month,
                Revenue = revenue,
                Cost = cost,
                Expense = expense,
                Profit = profit
            });
        }

        return result;
    }

    /// <summary>
    /// 收入构成：查询损益类收入科目（5001下级），按科目名称分组汇总
    /// </summary>
    public async Task<List<CompositionItemDto>> GetRevenueCompositionAsync(int year, int month, long accountSetId)
    {
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();

        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var entries = await (from e in _voucherEntryRepository.Query()
                             join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                             where (v.FStatus == 2 || v.FStatus == 3) && v.FSource != VoucherSource.SystemClosing && v.FDate >= startDate && v.FDate <= endDate
                             select e).ToListAsync();

        var amounts = CalculateAccountAmounts(entries, accounts);

        // 收入科目：5001前缀的叶子科目
        var revenueAccounts = accounts.Where(a => a.FCode.StartsWith("5001") && a.FIsLeaf == 1).ToList();
        var items = revenueAccounts
            .Select(a => new { Name = a.FName, Value = amounts.ContainsKey(a.FCode) ? amounts[a.FCode] : 0 })
            .Where(x => x.Value != 0)
            .ToList();

        var total = items.Sum(x => x.Value);
        return items.Select(x => new CompositionItemDto
        {
            Name = x.Name,
            Value = x.Value,
            Percentage = total != 0 ? Math.Round(x.Value / total * 100, 2) : 0
        }).OrderByDescending(x => x.Value).ToList();
    }

    /// <summary>
    /// 费用构成：查询费用类科目（5601、5602、5603），按大类汇总
    /// </summary>
    public async Task<List<CompositionItemDto>> GetExpenseCompositionAsync(int year, int month, long accountSetId)
    {
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();

        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var entries = await (from e in _voucherEntryRepository.Query()
                             join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                             where (v.FStatus == 2 || v.FStatus == 3) && v.FSource != VoucherSource.SystemClosing && v.FDate >= startDate && v.FDate <= endDate
                             select e).ToListAsync();

        var amounts = CalculateAccountAmounts(entries, accounts);

        var sellExp = GetAmountByCodePrefix(amounts, "5601");
        var mgmtExp = GetAmountByCodePrefix(amounts, "5602");
        var finExp = GetAmountByCodePrefix(amounts, "5603");
        var total = sellExp + mgmtExp + finExp;

        var items = new List<CompositionItemDto>();
        if (sellExp != 0) items.Add(new CompositionItemDto { Name = "销售费用", Value = sellExp, Percentage = total != 0 ? Math.Round(sellExp / total * 100, 2) : 0 });
        if (mgmtExp != 0) items.Add(new CompositionItemDto { Name = "管理费用", Value = mgmtExp, Percentage = total != 0 ? Math.Round(mgmtExp / total * 100, 2) : 0 });
        if (finExp != 0) items.Add(new CompositionItemDto { Name = "财务费用", Value = finExp, Percentage = total != 0 ? Math.Round(finExp / total * 100, 2) : 0 });

        return items;
    }

    /// <summary>
    /// 同比对比：本月 vs 去年同月
    /// </summary>
    public async Task<List<ComparisonDto>> GetYoYComparisonAsync(int year, int month, long accountSetId)
    {
        return await BuildComparisonAsync(year, month, year - 1, month, accountSetId);
    }

    /// <summary>
    /// 环比对比：本月 vs 上月
    /// </summary>
    public async Task<List<ComparisonDto>> GetMoMComparisonAsync(int year, int month, long accountSetId)
    {
        var prevYear = month == 1 ? year - 1 : year;
        var prevMonth = month == 1 ? 12 : month - 1;
        return await BuildComparisonAsync(year, month, prevYear, prevMonth, accountSetId);
    }

    private async Task<List<ComparisonDto>> BuildComparisonAsync(int curYear, int curMonth, int prevYear, int prevMonth, long accountSetId)
    {
        var accounts = await _accountRepository.Query()
            .Where(a => a.FAccountSetId == accountSetId)
            .ToListAsync();

        async Task<Dictionary<string, decimal>> GetMonthAmounts(int y, int m)
        {
            var start = new DateTime(y, m, 1);
            var end = start.AddMonths(1).AddDays(-1);
            var entries = await (from e in _voucherEntryRepository.Query()
                                 join v in _voucherRepository.Query().Where(v => v.FAccountSetId == accountSetId) on e.FVoucherId equals v.FID
                                 where (v.FStatus == 2 || v.FStatus == 3) && v.FSource != VoucherSource.SystemClosing && v.FDate >= start && v.FDate <= end
                                 select e).ToListAsync();
            return CalculateAccountAmounts(entries, accounts);
        }

        var curAmounts = await GetMonthAmounts(curYear, curMonth);
        var prevAmounts = await GetMonthAmounts(prevYear, prevMonth);

        var indicators = new (string Name, Func<Dictionary<string, decimal>, decimal> Calc)[]
        {
            ("营业收入", a => GetAmountByCodePrefix(a, "5001") + GetAmountByCodePrefix(a, "5051")),
            ("营业成本", a => GetAmountByCodePrefix(a, "5401") + GetAmountByCodePrefix(a, "5402")),
            ("销售费用", a => GetAmountByCodePrefix(a, "5601")),
            ("管理费用", a => GetAmountByCodePrefix(a, "5602")),
            ("财务费用", a => GetAmountByCodePrefix(a, "5603")),
            ("净利润", a => {
                var rev = GetAmountByCodePrefix(a, "5001") + GetAmountByCodePrefix(a, "5051");
                var cost = GetAmountByCodePrefix(a, "5401") + GetAmountByCodePrefix(a, "5402");
                var exp = GetAmountByCodePrefix(a, "5601") + GetAmountByCodePrefix(a, "5602") + GetAmountByCodePrefix(a, "5603");
                return rev - cost - exp;
            })
        };

        return indicators.Select(ind =>
        {
            var cur = ind.Calc(curAmounts);
            var prev = ind.Calc(prevAmounts);
            var change = cur - prev;
            var rate = prev != 0 ? Math.Round(change / Math.Abs(prev) * 100, 2) : 0;
            return new ComparisonDto
            {
                ItemName = ind.Name,
                CurrentValue = cur,
                PreviousValue = prev,
                ChangeAmount = change,
                ChangeRate = rate
            };
        }).ToList();
    }
}
