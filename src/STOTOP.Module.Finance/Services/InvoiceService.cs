using Microsoft.EntityFrameworkCore;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IRepository<FinInvoice> _invoiceRepository;
    private readonly IRepository<FinVoucher> _voucherRepository;
    private readonly IRepository<FinVoucherEntry> _entryRepository;
    private readonly IRepository<FinAccount> _accountRepository;
    private readonly IRepository<FinAccountPeriod> _periodRepository;
    private readonly OperationLogService _operationLogService;

    public InvoiceService(
        IRepository<FinInvoice> invoiceRepository,
        IRepository<FinVoucher> voucherRepository,
        IRepository<FinVoucherEntry> entryRepository,
        IRepository<FinAccount> accountRepository,
        IRepository<FinAccountPeriod> periodRepository,
        OperationLogService operationLogService)
    {
        _invoiceRepository = invoiceRepository;
        _voucherRepository = voucherRepository;
        _entryRepository = entryRepository;
        _accountRepository = accountRepository;
        _periodRepository = periodRepository;
        _operationLogService = operationLogService;
    }

    public async Task<int> ImportInvoicesAsync(Stream fileStream, long accountSetId)
    {
        var workbook = CreateWorkbook(fileStream);
        var sheet = workbook.GetSheetAt(0);
        if (sheet == null) throw new InvalidOperationException("Excel文件中没有工作表");

        // 读取表头，建立列名映射
        var headerRow = sheet.GetRow(0);
        if (headerRow == null) throw new InvalidOperationException("Excel文件缺少表头行");

        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < headerRow.LastCellNum; c++)
        {
            var cell = headerRow.GetCell(c);
            if (cell != null)
            {
                var name = cell.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(name))
                    columnMap[name] = c;
            }
        }

        var batchId = DateTime.Now.Ticks;
        int importCount = 0;

        for (int i = 1; i <= sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row == null) continue;

            var invoiceNo = GetCellString(row, columnMap, "发票号码");
            if (string.IsNullOrWhiteSpace(invoiceNo)) continue;

            var invoice = new FinInvoice
            {
                FAccountSetId = accountSetId,
                FInvoiceType = GetCellString(row, columnMap, "发票类型"),
                FInvoiceNo = invoiceNo,
                FInvoiceCode = GetCellString(row, columnMap, "发票代码"),
                FInvoiceDate = GetCellDate(row, columnMap, "开票日期"),
                FSellerName = GetCellString(row, columnMap, "销方名称"),
                FSellerTaxNo = GetCellString(row, columnMap, "销方税号"),
                FBuyerName = GetCellString(row, columnMap, "购方名称"),
                FBuyerTaxNo = GetCellString(row, columnMap, "购方税号"),
                FAmount = GetCellDecimal(row, columnMap, "金额"),
                FTaxAmount = GetCellDecimal(row, columnMap, "税额"),
                FTotalAmount = GetCellDecimal(row, columnMap, "价税合计"),
                FTaxRate = GetCellDecimal(row, columnMap, "税率"),
                FDirection = GetCellString(row, columnMap, "方向"),
                FMatchStatus = 0,
                FImportBatchId = batchId,
                FStatus = 1,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };

            // 如果价税合计为0，则自动计算
            if (invoice.FTotalAmount == 0)
                invoice.FTotalAmount = invoice.FAmount + invoice.FTaxAmount;

            // 如果方向为空，尝试根据发票类型推断
            if (string.IsNullOrEmpty(invoice.FDirection))
            {
                invoice.FDirection = invoice.FInvoiceType.Contains("销") ? "销项" : "进项";
            }

            await _invoiceRepository.AddAsync(invoice);
            importCount++;
        }

        await _operationLogService.LogAsync(
            accountSetId, "发票", "导入",
            $"导入发票 {importCount} 张",
            batchId, $"批次{batchId}");

        return importCount;
    }

    public async Task<InvoicePagedResult> GetInvoicesAsync(InvoiceQueryRequest request, long accountSetId)
    {
        var query = _invoiceRepository.Query()
            .Where(inv => inv.FAccountSetId == accountSetId);

        if (!string.IsNullOrEmpty(request.InvoiceType))
            query = query.Where(inv => inv.FInvoiceType == request.InvoiceType);

        if (request.StartDate.HasValue)
            query = query.Where(inv => inv.FInvoiceDate >= request.StartDate.Value);

        if (request.EndDate.HasValue)
        {
            var endDateExclusive = request.EndDate.Value.Date.AddDays(1);
            query = query.Where(inv => inv.FInvoiceDate < endDateExclusive);
        }

        if (!string.IsNullOrEmpty(request.Direction))
            query = query.Where(inv => inv.FDirection == request.Direction);

        if (request.MatchStatus.HasValue)
            query = query.Where(inv => inv.FMatchStatus == request.MatchStatus.Value);

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(inv =>
                inv.FInvoiceNo.Contains(request.Keyword) ||
                (inv.FSellerName != null && inv.FSellerName.Contains(request.Keyword)) ||
                (inv.FBuyerName != null && inv.FBuyerName.Contains(request.Keyword)));

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(inv => inv.FInvoiceDate)
            .ThenByDescending(inv => inv.FID)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new InvoicePagedResult
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<InvoiceDto?> GetInvoiceByIdAsync(long id)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(id);
        return invoice == null ? null : MapToDto(invoice);
    }

    public async Task<bool> MatchInvoiceAsync(long invoiceId, long voucherId, long accountSetId)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId);
        if (invoice == null || invoice.FAccountSetId != accountSetId)
            return false;

        var voucher = await _voucherRepository.GetByIdAsync(voucherId);
        if (voucher == null) return false;

        invoice.FMatchedVoucherId = voucherId;
        invoice.FMatchStatus = 1;
        invoice.FUpdatedTime = DateTime.Now;
        await _invoiceRepository.UpdateAsync(invoice);

        await _operationLogService.LogAsync(
            accountSetId, "发票", "匹配",
            $"发票 {invoice.FInvoiceNo} 匹配凭证 {voucher.FVoucherWord}{voucher.FVoucherNo}",
            invoiceId, invoice.FInvoiceNo);

        return true;
    }

    public async Task<VoucherDto> GenerateVoucherFromInvoiceAsync(long invoiceId, long accountSetId, InvoiceGenerateVoucherRequest request)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId);
        if (invoice == null || invoice.FAccountSetId != accountSetId)
            throw new InvalidOperationException("发票不存在");

        if (invoice.FMatchStatus == 1)
            throw new InvalidOperationException("该发票已匹配凭证，不能重复生成");

        // 获取下一个凭证号
        var maxNo = await _voucherRepository.Query()
            .Where(v => v.FVoucherWord == request.VoucherWord && v.FPeriodId == request.PeriodId && v.FAccountSetId == accountSetId)
            .MaxAsync(v => (int?)v.FVoucherNo) ?? 0;
        var nextNo = maxNo + 1;

        var voucher = new FinVoucher
        {
            FVoucherWord = request.VoucherWord,
            FVoucherNo = nextNo,
            FDate = invoice.FInvoiceDate,
            FPeriodId = request.PeriodId,
            FAttachmentCount = 0,
            FCreator = "system",
            FStatus = 1,
            FSource = "发票导入",
            FRemark = $"由发票 {invoice.FInvoiceNo} 自动生成",
            FAccountSetId = accountSetId,
            FOrgId = 0,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _voucherRepository.AddAsync(voucher);

        // 构建凭证分录
        var entries = await BuildVoucherEntries(invoice, voucher.FID, accountSetId);
        foreach (var entry in entries)
        {
            await _entryRepository.AddAsync(entry);
        }

        // 关联发票和凭证
        invoice.FMatchedVoucherId = voucher.FID;
        invoice.FMatchStatus = 1;
        invoice.FUpdatedTime = DateTime.Now;
        await _invoiceRepository.UpdateAsync(invoice);

        await _operationLogService.LogAsync(
            accountSetId, "发票", "生成凭证",
            $"发票 {invoice.FInvoiceNo} 生成凭证 {voucher.FVoucherWord}{voucher.FVoucherNo}",
            voucher.FID, $"{voucher.FVoucherWord}{voucher.FVoucherNo}");

        // 返回凭证DTO
        var createdVoucher = await _voucherRepository.Query()
            .Include(v => v.Entries)
            .FirstOrDefaultAsync(v => v.FID == voucher.FID);

        return MapToVoucherDto(createdVoucher!);
    }

    public async Task<List<TaxSummaryDto>> GetTaxSummaryAsync(int year, long accountSetId)
    {
        var startDate = new DateTime(year, 1, 1);
        var endDate = new DateTime(year + 1, 1, 1);

        var invoices = await _invoiceRepository.Query()
            .Where(inv => inv.FAccountSetId == accountSetId
                && inv.FInvoiceDate >= startDate
                && inv.FInvoiceDate < endDate)
            .ToListAsync();

        var result = new List<TaxSummaryDto>();
        for (int month = 1; month <= 12; month++)
        {
            var monthInvoices = invoices.Where(inv => inv.FInvoiceDate.Month == month).ToList();
            var inputTax = monthInvoices.Where(inv => inv.FDirection == "进项").Sum(inv => inv.FTaxAmount);
            var outputTax = monthInvoices.Where(inv => inv.FDirection == "销项").Sum(inv => inv.FTaxAmount);

            result.Add(new TaxSummaryDto
            {
                Month = month,
                InputTaxAmount = inputTax,
                OutputTaxAmount = outputTax,
                TaxPayable = outputTax - inputTax
            });
        }

        return result;
    }

    #region 私有方法

    private async Task<List<FinVoucherEntry>> BuildVoucherEntries(FinInvoice invoice, long voucherId, long accountSetId)
    {
        var entries = new List<FinVoucherEntry>();
        var now = DateTime.Now;

        if (invoice.FDirection == "进项")
        {
            // 进项发票：借 原材料/库存商品(1401/1405) + 借 应交税费-进项税额(222101)，贷 应付账款(2202)
            var materialAccount = await FindAccountByCode("1405", accountSetId)
                ?? await FindAccountByCode("1401", accountSetId);
            var taxAccount = await FindAccountByCode("222101", accountSetId);
            var payableAccount = await FindAccountByCode("2202", accountSetId);

            entries.Add(new FinVoucherEntry
            {
                FVoucherId = voucherId,
                FLineNo = 1,
                FSummary = $"购入 {invoice.FSellerName}",
                FAccountId = materialAccount?.FID ?? 0,
                FAccountCode = materialAccount?.FCode ?? "1405",
                FAccountName = materialAccount?.FName ?? "库存商品",
                FDebitAmount = invoice.FAmount,
                FCreditAmount = 0,
                FCreatedTime = now,
                FUpdatedTime = now
            });

            entries.Add(new FinVoucherEntry
            {
                FVoucherId = voucherId,
                FLineNo = 2,
                FSummary = $"进项税额 {invoice.FSellerName}",
                FAccountId = taxAccount?.FID ?? 0,
                FAccountCode = taxAccount?.FCode ?? "222101",
                FAccountName = taxAccount?.FName ?? "应交税费-应交增值税-进项税额",
                FDebitAmount = invoice.FTaxAmount,
                FCreditAmount = 0,
                FCreatedTime = now,
                FUpdatedTime = now
            });

            entries.Add(new FinVoucherEntry
            {
                FVoucherId = voucherId,
                FLineNo = 3,
                FSummary = $"应付 {invoice.FSellerName}",
                FAccountId = payableAccount?.FID ?? 0,
                FAccountCode = payableAccount?.FCode ?? "2202",
                FAccountName = payableAccount?.FName ?? "应付账款",
                FDebitAmount = 0,
                FCreditAmount = invoice.FTotalAmount,
                FCreatedTime = now,
                FUpdatedTime = now
            });
        }
        else
        {
            // 销项发票：借 应收账款(1122)，贷 主营业务收入(5001) + 贷 应交税费-销项税额(222102)
            var receivableAccount = await FindAccountByCode("1122", accountSetId);
            var revenueAccount = await FindAccountByCode("5001", accountSetId);
            var taxAccount = await FindAccountByCode("222102", accountSetId);

            entries.Add(new FinVoucherEntry
            {
                FVoucherId = voucherId,
                FLineNo = 1,
                FSummary = $"应收 {invoice.FBuyerName}",
                FAccountId = receivableAccount?.FID ?? 0,
                FAccountCode = receivableAccount?.FCode ?? "1122",
                FAccountName = receivableAccount?.FName ?? "应收账款",
                FDebitAmount = invoice.FTotalAmount,
                FCreditAmount = 0,
                FCreatedTime = now,
                FUpdatedTime = now
            });

            entries.Add(new FinVoucherEntry
            {
                FVoucherId = voucherId,
                FLineNo = 2,
                FSummary = $"销售收入 {invoice.FBuyerName}",
                FAccountId = revenueAccount?.FID ?? 0,
                FAccountCode = revenueAccount?.FCode ?? "5001",
                FAccountName = revenueAccount?.FName ?? "主营业务收入",
                FDebitAmount = 0,
                FCreditAmount = invoice.FAmount,
                FCreatedTime = now,
                FUpdatedTime = now
            });

            entries.Add(new FinVoucherEntry
            {
                FVoucherId = voucherId,
                FLineNo = 3,
                FSummary = $"销项税额 {invoice.FBuyerName}",
                FAccountId = taxAccount?.FID ?? 0,
                FAccountCode = taxAccount?.FCode ?? "222102",
                FAccountName = taxAccount?.FName ?? "应交税费-应交增值税-销项税额",
                FDebitAmount = 0,
                FCreditAmount = invoice.FTaxAmount,
                FCreatedTime = now,
                FUpdatedTime = now
            });
        }

        return entries;
    }

    private async Task<FinAccount?> FindAccountByCode(string code, long accountSetId)
    {
        return await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.FCode == code && a.FAccountSetId == accountSetId);
    }

    private static IWorkbook CreateWorkbook(Stream stream)
    {
        try
        {
            return new XSSFWorkbook(stream);
        }
        catch
        {
            stream.Position = 0;
            return new HSSFWorkbook(stream);
        }
    }

    private static string GetCellString(IRow row, Dictionary<string, int> columnMap, string columnName)
    {
        if (!columnMap.TryGetValue(columnName, out var colIndex)) return string.Empty;
        var cell = row.GetCell(colIndex);
        if (cell == null) return string.Empty;
        if (cell.CellType == CellType.Numeric)
            return cell.NumericCellValue.ToString();
        return cell.ToString()?.Trim() ?? string.Empty;
    }

    private static decimal GetCellDecimal(IRow row, Dictionary<string, int> columnMap, string columnName)
    {
        if (!columnMap.TryGetValue(columnName, out var colIndex)) return 0;
        var cell = row.GetCell(colIndex);
        if (cell == null) return 0;
        if (cell.CellType == CellType.Numeric)
            return (decimal)cell.NumericCellValue;
        if (decimal.TryParse(cell.ToString()?.Trim(), out var val))
            return val;
        return 0;
    }

    private static DateTime GetCellDate(IRow row, Dictionary<string, int> columnMap, string columnName)
    {
        if (!columnMap.TryGetValue(columnName, out var colIndex)) return DateTime.Today;
        var cell = row.GetCell(colIndex);
        if (cell == null) return DateTime.Today;
        if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
            return cell.DateCellValue ?? DateTime.Today;
        if (DateTime.TryParse(cell.ToString()?.Trim(), out var date))
            return date;
        return DateTime.Today;
    }

    private static InvoiceDto MapToDto(FinInvoice inv)
    {
        return new InvoiceDto
        {
            Id = inv.FID,
            AccountSetId = inv.FAccountSetId,
            InvoiceType = inv.FInvoiceType,
            InvoiceNo = inv.FInvoiceNo,
            InvoiceCode = inv.FInvoiceCode,
            InvoiceDate = inv.FInvoiceDate,
            SellerName = inv.FSellerName,
            SellerTaxNo = inv.FSellerTaxNo,
            BuyerName = inv.FBuyerName,
            BuyerTaxNo = inv.FBuyerTaxNo,
            Amount = inv.FAmount,
            TaxAmount = inv.FTaxAmount,
            TotalAmount = inv.FTotalAmount,
            TaxRate = inv.FTaxRate,
            Direction = inv.FDirection,
            MatchStatus = inv.FMatchStatus,
            MatchedVoucherId = inv.FMatchedVoucherId,
            ImportBatchId = inv.FImportBatchId,
            Status = inv.FStatus,
            CreatedTime = inv.FCreatedTime
        };
    }

    private static VoucherDto MapToVoucherDto(FinVoucher v)
    {
        return new VoucherDto
        {
            Id = v.FID,
            VoucherWord = v.FVoucherWord,
            VoucherNo = v.FVoucherNo,
            Date = v.FDate,
            PeriodId = v.FPeriodId,
            Creator = v.FCreator,
            Status = v.FStatus,
            Source = v.FSource,
            Remark = v.FRemark,
            TotalDebit = v.Entries.Sum(e => e.FDebitAmount),
            TotalCredit = v.Entries.Sum(e => e.FCreditAmount),
            Entries = v.Entries.OrderBy(e => e.FLineNo).Select(e => new VoucherEntryDto
            {
                Id = e.FID,
                LineNo = e.FLineNo,
                Summary = e.FSummary,
                AccountId = e.FAccountId,
                AccountCode = e.FAccountCode,
                AccountName = e.FAccountName,
                DebitAmount = e.FDebitAmount,
                CreditAmount = e.FCreditAmount
            }).ToList()
        };
    }

    #endregion
}
