using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class BankReconciliationService : IBankReconciliationService
{
    private readonly IRepository<FinBankStatement> _statementRepository;
    private readonly IRepository<FinBankReconciliation> _reconciliationRepository;
    private readonly IRepository<FinVoucher> _voucherRepository;
    private readonly IRepository<FinVoucherEntry> _entryRepository;
    private readonly IRepository<FinAccountPeriod> _periodRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BankReconciliationService(
        IRepository<FinBankStatement> statementRepository,
        IRepository<FinBankReconciliation> reconciliationRepository,
        IRepository<FinVoucher> voucherRepository,
        IRepository<FinVoucherEntry> entryRepository,
        IRepository<FinAccountPeriod> periodRepository,
        IHttpContextAccessor httpContextAccessor)
    {
        _statementRepository = statementRepository;
        _reconciliationRepository = reconciliationRepository;
        _voucherRepository = voucherRepository;
        _entryRepository = entryRepository;
        _periodRepository = periodRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    private long GetCurrentUserId()
    {
        var userIdStr = _httpContextAccessor.HttpContext?.User
            .FindFirst(global::System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userIdStr, out var userId)) return userId;
        return 0;
    }

    public async Task<int> ImportBankStatementsAsync(Stream fileStream, BankStatementImportRequest request, long accountSetId)
    {
        var workbook = OpenWorkbook(fileStream);
        var sheet = workbook.GetSheetAt(0);
        if (sheet == null)
            throw new InvalidOperationException("Excel文件中没有工作表");

        var batchId = DateTime.Now.Ticks;
        var importCount = 0;
        var startRowIndex = request.StartRow - 1; // 转为0-based

        for (int i = startRowIndex; i <= sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row == null) continue;

            var dateStr = GetCellStringValue(row, request.DateColumnIndex);
            if (string.IsNullOrWhiteSpace(dateStr)) continue;

            if (!TryParseDate(row, request.DateColumnIndex, out var transactionDate))
                continue;

            var statement = new FinBankStatement
            {
                FAccountSetId = accountSetId,
                FBankAccount = request.BankAccount,
                FBankName = request.BankName,
                FTransactionDate = transactionDate,
                FDescription = GetCellStringValue(row, request.DescriptionColumnIndex),
                FDebitAmount = GetCellDecimalValue(row, request.DebitColumnIndex),
                FCreditAmount = GetCellDecimalValue(row, request.CreditColumnIndex),
                FBalance = GetCellDecimalValue(row, request.BalanceColumnIndex),
                FCounterparty = request.CounterpartyColumnIndex >= 0
                    ? GetCellStringValue(row, request.CounterpartyColumnIndex) : null,
                FReferenceNo = request.ReferenceNoColumnIndex >= 0
                    ? GetCellStringValue(row, request.ReferenceNoColumnIndex) : null,
                FMatchStatus = 0,
                FImportBatchId = batchId,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now,
            };

            await _statementRepository.AddAsync(statement);
            importCount++;
        }

        return importCount;
    }

    public async Task<BankStatementPagedResult> GetStatementsAsync(BankStatementQueryRequest request, long accountSetId)
    {
        var query = _statementRepository.Query()
            .Where(s => s.FAccountSetId == accountSetId);

        if (request.StartDate.HasValue)
            query = query.Where(s => s.FTransactionDate >= request.StartDate.Value.Date);

        if (request.EndDate.HasValue)
        {
            var endDateExclusive = request.EndDate.Value.Date.AddDays(1);
            query = query.Where(s => s.FTransactionDate < endDateExclusive);
        }

        if (request.MatchStatus.HasValue)
            query = query.Where(s => s.FMatchStatus == request.MatchStatus.Value);

        if (!string.IsNullOrEmpty(request.BankAccount))
            query = query.Where(s => s.FBankAccount == request.BankAccount);

        var totalQuery = _statementRepository.Query()
            .Where(s => s.FAccountSetId == accountSetId);
        if (request.StartDate.HasValue)
            totalQuery = totalQuery.Where(s => s.FTransactionDate >= request.StartDate.Value.Date);
        if (request.EndDate.HasValue)
            totalQuery = totalQuery.Where(s => s.FTransactionDate < request.EndDate.Value.Date.AddDays(1));
        if (!string.IsNullOrEmpty(request.BankAccount))
            totalQuery = totalQuery.Where(s => s.FBankAccount == request.BankAccount);

        var matchedCount = await totalQuery.CountAsync(s => s.FMatchStatus == 1);
        var unmatchedCount = await totalQuery.CountAsync(s => s.FMatchStatus == 0);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.FTransactionDate)
            .ThenByDescending(s => s.FID)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new BankStatementPagedResult
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            MatchedCount = matchedCount,
            UnmatchedCount = unmatchedCount,
        };
    }

    public async Task<List<UnmatchedVoucherDto>> GetUnmatchedVouchersAsync(DateTime startDate, DateTime endDate, long accountSetId)
    {
        var endDateExclusive = endDate.Date.AddDays(1);

        // 获取已匹配的凭证分录ID
        var matchedEntryIds = await _reconciliationRepository.Query()
            .Where(r => r.FAccountSetId == accountSetId && r.FVoucherEntryId.HasValue)
            .Select(r => r.FVoucherEntryId!.Value)
            .ToListAsync();

        // 查询银行存款科目(1002开头)的凭证分录
        var entries = await _entryRepository.Query()
            .Where(e => e.FAccountCode.StartsWith("1002"))
            .Join(
                _voucherRepository.Query()
                    .Where(v => v.FAccountSetId == accountSetId
                        && v.FDate >= startDate.Date
                        && v.FDate < endDateExclusive
                        && v.FStatus >= 1), // 非草稿
                e => e.FVoucherId,
                v => v.FID,
                (e, v) => new { Entry = e, Voucher = v }
            )
            .Where(x => !matchedEntryIds.Contains(x.Entry.FID))
            .OrderByDescending(x => x.Voucher.FDate)
            .ThenBy(x => x.Voucher.FVoucherNo)
            .ToListAsync();

        return entries.Select(x => new UnmatchedVoucherDto
        {
            VoucherId = x.Voucher.FID,
            EntryId = x.Entry.FID,
            Date = x.Voucher.FDate,
            VoucherNo = $"{x.Voucher.FVoucherWord}{x.Voucher.FVoucherNo}",
            Summary = x.Entry.FSummary,
            AccountCode = x.Entry.FAccountCode,
            AccountName = x.Entry.FAccountName,
            DebitAmount = x.Entry.FDebitAmount,
            CreditAmount = x.Entry.FCreditAmount,
        }).ToList();
    }

    public async Task<int> AutoMatchAsync(long accountSetId)
    {
        var unmatchedStatements = await _statementRepository.Query()
            .Where(s => s.FAccountSetId == accountSetId && s.FMatchStatus == 0)
            .ToListAsync();

        // 获取已匹配的凭证分录ID
        var matchedEntryIds = await _reconciliationRepository.Query()
            .Where(r => r.FAccountSetId == accountSetId && r.FVoucherEntryId.HasValue)
            .Select(r => r.FVoucherEntryId!.Value)
            .ToListAsync();
        var matchedEntryIdSet = new HashSet<long>(matchedEntryIds);

        // 获取所有银行存款科目的凭证分录
        var bankEntries = await _entryRepository.Query()
            .Where(e => e.FAccountCode.StartsWith("1002"))
            .Join(
                _voucherRepository.Query()
                    .Where(v => v.FAccountSetId == accountSetId && v.FStatus >= 1),
                e => e.FVoucherId,
                v => v.FID,
                (e, v) => new { Entry = e, Voucher = v }
            )
            .ToListAsync();

        // 过滤已匹配的
        var availableEntries = bankEntries
            .Where(x => !matchedEntryIdSet.Contains(x.Entry.FID))
            .ToList();

        var matchCount = 0;
        var now = DateTime.Now;
        var userId = GetCurrentUserId();
        var newlyMatchedEntryIds = new HashSet<long>();

        foreach (var statement in unmatchedStatements)
        {
            // 银行流水的金额：借方表示收入，贷方表示支出
            // 凭证分录中：借方表示银行收入(对应流水借方)，贷方表示银行支出(对应流水贷方)
            var amount = statement.FDebitAmount > 0 ? statement.FDebitAmount : statement.FCreditAmount;
            var isDebit = statement.FDebitAmount > 0;

            var matched = availableEntries.FirstOrDefault(x =>
                !newlyMatchedEntryIds.Contains(x.Entry.FID) &&
                Math.Abs((x.Voucher.FDate - statement.FTransactionDate).TotalDays) <= 3 &&
                ((isDebit && x.Entry.FDebitAmount == amount) ||
                 (!isDebit && x.Entry.FCreditAmount == amount))
            );

            if (matched != null)
            {
                var reconciliation = new FinBankReconciliation
                {
                    FAccountSetId = accountSetId,
                    FBankStatementId = statement.FID,
                    FVoucherId = matched.Voucher.FID,
                    FVoucherEntryId = matched.Entry.FID,
                    FMatchType = "auto",
                    FMatchTime = now,
                    FOperatorId = userId,
                };

                await _reconciliationRepository.AddAsync(reconciliation);

                statement.FMatchStatus = 1;
                statement.FMatchedVoucherId = matched.Voucher.FID;
                statement.FUpdatedTime = now;
                await _statementRepository.UpdateAsync(statement);

                newlyMatchedEntryIds.Add(matched.Entry.FID);
                matchCount++;
            }
        }

        return matchCount;
    }

    public async Task<bool> ManualMatchAsync(ManualMatchRequest request, long accountSetId)
    {
        var statement = await _statementRepository.GetByIdAsync(request.BankStatementId);
        if (statement == null)
            throw new InvalidOperationException("银行流水不存在");

        if (statement.FMatchStatus == 1)
            throw new InvalidOperationException("该银行流水已匹配");

        var now = DateTime.Now;
        var userId = GetCurrentUserId();

        var reconciliation = new FinBankReconciliation
        {
            FAccountSetId = accountSetId,
            FBankStatementId = request.BankStatementId,
            FVoucherId = request.VoucherId,
            FVoucherEntryId = request.VoucherEntryId,
            FMatchType = "manual",
            FMatchTime = now,
            FOperatorId = userId,
        };

        await _reconciliationRepository.AddAsync(reconciliation);

        statement.FMatchStatus = 1;
        statement.FMatchedVoucherId = request.VoucherId;
        statement.FUpdatedTime = now;
        await _statementRepository.UpdateAsync(statement);

        return true;
    }

    public async Task<bool> UnmatchAsync(long reconciliationId)
    {
        var reconciliation = await _reconciliationRepository.GetByIdAsync(reconciliationId);
        if (reconciliation == null)
            throw new InvalidOperationException("匹配记录不存在");

        var statement = await _statementRepository.GetByIdAsync(reconciliation.FBankStatementId);
        if (statement != null)
        {
            statement.FMatchStatus = 0;
            statement.FMatchedVoucherId = null;
            statement.FUpdatedTime = DateTime.Now;
            await _statementRepository.UpdateAsync(statement);
        }

        await _reconciliationRepository.DeleteAsync(reconciliationId);
        return true;
    }

    public async Task<ReconciliationReportDto> GetReconciliationReportAsync(long periodId, long accountSetId)
    {
        var period = await _periodRepository.GetByIdAsync(periodId);
        if (period == null)
            throw new InvalidOperationException("账期不存在");

        var startDate = period.FStartDate;
        var endDate = period.FEndDate;
        var endDateExclusive = endDate.Date.AddDays(1);

        // 银行流水余额 = 最后一笔流水的余额
        var lastStatement = await _statementRepository.Query()
            .Where(s => s.FAccountSetId == accountSetId && s.FTransactionDate < endDateExclusive)
            .OrderByDescending(s => s.FTransactionDate)
            .ThenByDescending(s => s.FID)
            .FirstOrDefaultAsync();
        var bankBalance = lastStatement?.FBalance ?? 0;

        // 未匹配的银行流水
        var unmatchedStatements = await _statementRepository.Query()
            .Where(s => s.FAccountSetId == accountSetId
                && s.FTransactionDate >= startDate
                && s.FTransactionDate < endDateExclusive
                && s.FMatchStatus == 0)
            .ToListAsync();

        // 银行已收企业未收（银行流水借方，未匹配）
        var bankReceivedItems = unmatchedStatements
            .Where(s => s.FDebitAmount > 0)
            .ToList();

        // 银行已付企业未付（银行流水贷方，未匹配）
        var bankPaidItems = unmatchedStatements
            .Where(s => s.FCreditAmount > 0)
            .ToList();

        // 未匹配的凭证分录
        var unmatchedVouchers = await GetUnmatchedVouchersAsync(startDate, endDate, accountSetId);

        // 企业已收银行未收（凭证借方，未匹配）
        var companyReceivedItems = unmatchedVouchers
            .Where(v => v.DebitAmount > 0)
            .ToList();

        // 企业已付银行未付（凭证贷方，未匹配）
        var companyPaidItems = unmatchedVouchers
            .Where(v => v.CreditAmount > 0)
            .ToList();

        // 计算账面余额：从凭证分录中计算银行存款科目余额
        var bookEntries = await _entryRepository.Query()
            .Where(e => e.FAccountCode.StartsWith("1002"))
            .Join(
                _voucherRepository.Query()
                    .Where(v => v.FAccountSetId == accountSetId
                        && v.FDate < endDateExclusive
                        && v.FStatus >= 1),
                e => e.FVoucherId,
                v => v.FID,
                (e, v) => e
            )
            .ToListAsync();
        var bookBalance = bookEntries.Sum(e => e.FDebitAmount) - bookEntries.Sum(e => e.FCreditAmount);

        var companyReceivedSum = companyReceivedItems.Sum(v => v.DebitAmount);
        var companyPaidSum = companyPaidItems.Sum(v => v.CreditAmount);
        var bankReceivedSum = bankReceivedItems.Sum(s => s.FDebitAmount);
        var bankPaidSum = bankPaidItems.Sum(s => s.FCreditAmount);

        return new ReconciliationReportDto
        {
            BankBalance = bankBalance,
            CompanyReceivedBankNot = companyReceivedSum,
            CompanyPaidBankNot = companyPaidSum,
            AdjustedBankBalance = bankBalance + companyReceivedSum - companyPaidSum,
            BookBalance = bookBalance,
            BankReceivedCompanyNot = bankReceivedSum,
            BankPaidCompanyNot = bankPaidSum,
            AdjustedBookBalance = bookBalance + bankReceivedSum - bankPaidSum,
            CompanyReceivedItems = companyReceivedItems,
            CompanyPaidItems = companyPaidItems,
            BankReceivedItems = bankReceivedItems.Select(MapToDto).ToList(),
            BankPaidItems = bankPaidItems.Select(MapToDto).ToList(),
        };
    }

    #region Helpers

    private static BankStatementDto MapToDto(FinBankStatement s) => new()
    {
        Id = s.FID,
        BankAccount = s.FBankAccount,
        BankName = s.FBankName,
        TransactionDate = s.FTransactionDate,
        Description = s.FDescription,
        DebitAmount = s.FDebitAmount,
        CreditAmount = s.FCreditAmount,
        Balance = s.FBalance,
        Counterparty = s.FCounterparty,
        ReferenceNo = s.FReferenceNo,
        MatchStatus = s.FMatchStatus,
        MatchedVoucherId = s.FMatchedVoucherId,
        ImportBatchId = s.FImportBatchId,
        CreatedTime = s.FCreatedTime,
    };

    private static IWorkbook OpenWorkbook(Stream stream)
    {
        // 将流拷贝到 MemoryStream 以支持 Seek
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        try
        {
            return new XSSFWorkbook(ms); // .xlsx
        }
        catch
        {
            ms.Position = 0;
            return new HSSFWorkbook(ms); // .xls
        }
    }

    private static string GetCellStringValue(IRow row, int colIndex)
    {
        if (colIndex < 0) return string.Empty;
        var cell = row.GetCell(colIndex);
        if (cell == null) return string.Empty;
        cell.SetCellType(CellType.String);
        return cell.StringCellValue?.Trim() ?? string.Empty;
    }

    private static decimal GetCellDecimalValue(IRow row, int colIndex)
    {
        if (colIndex < 0) return 0;
        var cell = row.GetCell(colIndex);
        if (cell == null) return 0;

        if (cell.CellType == CellType.Numeric)
            return (decimal)cell.NumericCellValue;

        var str = cell.ToString()?.Trim().Replace(",", "") ?? "";
        if (decimal.TryParse(str, out var val))
            return val;

        return 0;
    }

    private static bool TryParseDate(IRow row, int colIndex, out DateTime result)
    {
        result = DateTime.MinValue;
        if (colIndex < 0) return false;

        var cell = row.GetCell(colIndex);
        if (cell == null) return false;

        if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
        {
            result = cell.DateCellValue ?? DateTime.MinValue;
            return result != DateTime.MinValue;
        }

        var str = cell.ToString()?.Trim() ?? "";
        return DateTime.TryParse(str, out result);
    }

    #endregion
}
